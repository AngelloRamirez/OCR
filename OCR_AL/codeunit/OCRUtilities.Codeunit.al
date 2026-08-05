codeunit 56800 "OCR Utilities"
{
    Access = Public;
    Permissions = tabledata "OCR Reading" = rimd,
                  tabledata "Purchase Header" = rimd,
                  tabledata "Purchase Line" = rimd,
                  tabledata Vendor = r,
                  tabledata Item = r;

    [ServiceEnabled]
    procedure ProcessText(InputText: Text; FileName: Text): Boolean
    var
        OCRReading: Record "OCR Reading";
        EntryNo: Integer;
    begin
        if InputText = '' then
            exit(false);

        if OCRReading.FindLast() then
            EntryNo := OCRReading."Entry No" + 1
        else
            EntryNo := 1;

        OCRReading.Init();
        OCRReading."Entry No" := EntryNo;
        OCRReading."File Name" := CopyStr(FileName, 1, MaxStrLen(OCRReading."File Name"));
        SaveTextToBlob(OCRReading, InputText);
        OCRReading.Insert();

        // Procesar JSON estructurado para crear la Factura de Compra en BC
        CreatePurchaseInvoiceFromJson(OCRReading, InputText);

        exit(true);
    end;

    procedure SaveTextToBlob(var OCRReading: Record "OCR Reading"; NewText: Text)
    var
        OutStr: OutStream;
    begin
        Clear(OCRReading."OCR JSON Reading");
        OCRReading."OCR JSON Reading".CreateOutStream(OutStr, TextEncoding::UTF8);
        OutStr.WriteText(NewText);
    end;

    procedure CreatePurchaseInvoiceFromJson(var OCRReading: Record "OCR Reading"; JsonText: Text)
    var
        JObj: JsonObject;
        JLinesArray: JsonArray;
        JLineToken: JsonToken;
        JLineObj: JsonObject;
        Vendor: Record Vendor;
        PurchHeader: Record "Purchase Header";
        PurchLine: Record "Purchase Line";
        ItemRec: Record Item;
        VendorName: Text;
        VendorRfc: Text;
        InvoiceNo: Text;
        InvoiceDateText: Text;
        InvDate: Date;
        LineNo: Integer;
        i: Integer;
        ItemNo: Text;
        ClaveSat: Text;
        LineDesc: Text;
        LineQty: Decimal;
        LineUnitPrice: Decimal;
        LineAmount: Decimal;
        FoundItem: Boolean;
    begin
        // 1. Validar que la cadena enviada sea un JSON parseable
        if not JObj.ReadFrom(JsonText) then begin
            SetReadingError(OCRReading, 'El texto recibido no es un JSON válido.');
            exit;
        end;

        VendorName := GetJsonValueAsText(JObj, 'vendorName');
        VendorRfc := GetJsonValueAsText(JObj, 'vendorRfc');
        InvoiceNo := GetJsonValueAsText(JObj, 'invoiceNo');
        InvoiceDateText := GetJsonValueAsText(JObj, 'invoiceDate');

        // 2. Localizar el Proveedor en BC
        if not FindVendorByRfcOrName(VendorRfc, VendorName, Vendor) then begin
            SetReadingError(OCRReading, StrSubstNo('Proveedor no encontrado. RFC: "%1", Nombre: "%2".', VendorRfc, VendorName));
            exit;
        end;

        // 3. Validar presencia de líneas
        if not GetJsonArray(JObj, 'lines', JLinesArray) or (JLinesArray.Count() = 0) then begin
            SetReadingError(OCRReading, 'El JSON no contiene líneas de detalle válidas (lines[]).');
            exit;
        end;

        // 4. Crear Encabezado de Factura de Compra (Purchase Header)
        PurchHeader.Init();
        PurchHeader."Document Type" := PurchHeader."Document Type"::Invoice;
        PurchHeader."No." := '';
        PurchHeader.Insert(true);

        PurchHeader.Validate("Buy-from Vendor No.", Vendor."No.");

        if InvoiceNo <> '' then
            PurchHeader.Validate("Vendor Invoice No.", CopyStr(InvoiceNo, 1, MaxStrLen(PurchHeader."Vendor Invoice No.")));

        if ParseIsoDate(InvoiceDateText, InvDate) then begin
            PurchHeader.Validate("Document Date", InvDate);
            PurchHeader.Validate("Posting Date", InvDate);
        end;

        PurchHeader.Modify(true);

        // 5. Crear Líneas de Factura de Compra (Purchase Lines)
        LineNo := 10000;
        for i := 0 to JLinesArray.Count() - 1 do begin
            JLinesArray.Get(i, JLineToken);
            if JLineToken.IsObject() then begin
                JLineObj := JLineToken.AsObject();
                ItemNo := GetJsonValueAsText(JLineObj, 'itemNo');
                ClaveSat := GetJsonValueAsText(JLineObj, 'claveSAT');
                if ClaveSat = '' then
                    ClaveSat := GetJsonValueAsText(JLineObj, 'claveSat');

                LineDesc := GetJsonValueAsText(JLineObj, 'description');
                LineQty := GetJsonValueAsDecimal(JLineObj, 'quantity');
                LineUnitPrice := GetJsonValueAsDecimal(JLineObj, 'unitPrice');
                LineAmount := GetJsonValueAsDecimal(JLineObj, 'lineAmount');

                // Búsqueda del artículo (Record Item)
                FoundItem := false;
                Clear(ItemRec);

                // 5.1 Buscar coincidencia exacta por ItemNo (No. de Artículo)
                if ItemNo <> '' then begin
                    ItemRec.Reset();
                    if ItemRec.Get(ItemNo) then
                        FoundItem := true
                    else begin
                        ItemRec.SetRange("No.", ItemNo);
                        if ItemRec.FindFirst() then
                            FoundItem := true;
                    end;
                end;

                // 5.2 Si no se encuentra, buscar por Clave SAT ("SAT Item Classification")
                if (not FoundItem) and (ClaveSat <> '') then begin
                    ItemRec.Reset();
                    ItemRec.SetFilter("SAT Item Classification", '@*' + DelChr(ClaveSat, '=', ' ') + '*');
                    if ItemRec.FindFirst() then
                        FoundItem := true;
                end;

                PurchLine.Init();
                PurchLine."Document Type" := PurchHeader."Document Type";
                PurchLine."Document No." := PurchHeader."No.";
                PurchLine."Line No." := LineNo;

                if FoundItem then begin
                    PurchLine.Validate(Type, PurchLine.Type::Item);
                    PurchLine.Validate("No.", ItemRec."No.");

                    // Si la descripción vino vacía en el OCR, tomar la descripción del Artículo
                    if LineDesc <> '' then
                        PurchLine.Validate(Description, CopyStr(LineDesc, 1, MaxStrLen(PurchLine.Description)))
                    else if ItemRec.Description <> '' then
                        PurchLine.Validate(Description, ItemRec.Description);

                    if LineQty > 0 then
                        PurchLine.Validate(Quantity, LineQty);

                    // Si el precio unitario vino en el OCR usarlo; de lo contrario obtener el del artículo
                    if LineUnitPrice > 0 then
                        PurchLine.Validate("Direct Unit Cost", LineUnitPrice)
                    else if ItemRec."Unit Cost" > 0 then
                        PurchLine.Validate("Direct Unit Cost", ItemRec."Unit Cost");
                end else begin
                    PurchLine.Validate(Type, PurchLine.Type::"G/L Account");
                    if LineDesc <> '' then
                        PurchLine.Validate(Description, CopyStr(LineDesc, 1, MaxStrLen(PurchLine.Description)));

                    if LineQty > 0 then
                        PurchLine.Validate(Quantity, LineQty);

                    if LineUnitPrice > 0 then
                        PurchLine.Validate("Direct Unit Cost", LineUnitPrice);
                end;

                PurchLine.Insert(true);
                LineNo += 10000;
            end;
        end;

        // 6. Actualizar registro OCR Reading con el documento creado exitosamente
        OCRReading."Document Created" := true;
        OCRReading."Document No." := PurchHeader."No.";
        OCRReading."Document Type" := OCRReading."Document Type"::Invoice;
        OCRReading."Error Message" := '';
        OCRReading.Modify();
    end;

    local procedure SetReadingError(var OCRReading: Record "OCR Reading"; ErrorMsg: Text)
    begin
        OCRReading."Document Created" := false;
        OCRReading."Document No." := '';
        OCRReading."Error Message" := CopyStr(ErrorMsg, 1, MaxStrLen(OCRReading."Error Message"));
        OCRReading.Modify();
    end;

    local procedure FindVendorByRfcOrName(RfcNo: Text; VendorName: Text; var Vendor: Record Vendor): Boolean
    var
        VendorRecRef: RecordRef;
    begin
        Vendor.Reset();

        // 1. Intentar buscar por el campo "RFC No." o "VAT Registration No."
        if (RfcNo <> '') then begin
            VendorRecRef.Open(Database::Vendor);
            if VendorRecRef.FieldExist(10000) or VendorRecRef.FieldExist(50000) then begin
                Vendor.SetFilter(Name, '@*' + VendorName + '*');
            end;
            VendorRecRef.Close();

            Vendor.SetRange("VAT Registration No.", RfcNo);
            if Vendor.FindFirst() then
                exit(true);
        end;

        // 2. Intentar buscar por Nombre/Razón social
        if VendorName <> '' then begin
            Vendor.Reset();
            Vendor.SetFilter(Name, '@*' + VendorName + '*');
            if Vendor.FindFirst() then
                exit(true);
        end;

        // 3. Fallback: Tomar el primer proveedor si existe alguno configurado
        Vendor.Reset();
        if Vendor.FindFirst() then
            exit(true);

        exit(false);
    end;

    local procedure ParseIsoDate(DateText: Text; var ResultDate: Date): Boolean
    var
        YearVal, MonthVal, DayVal : Integer;
    begin
        if StrLen(DateText) >= 10 then begin
            if Evaluate(YearVal, CopyStr(DateText, 1, 4)) and
               Evaluate(MonthVal, CopyStr(DateText, 6, 2)) and
               Evaluate(DayVal, CopyStr(DateText, 9, 2)) then begin
                ResultDate := DMY2Date(DayVal, MonthVal, YearVal);
                exit(true);
            end;
        end;
        exit(false);
    end;

    local procedure GetJsonValueAsText(JObject: JsonObject; PropertyName: Text): Text
    var
        JToken: JsonToken;
    begin
        if JObject.Get(PropertyName, JToken) then
            if JToken.IsValue() and (not JToken.AsValue().IsNull()) then
                exit(JToken.AsValue().AsText());
        exit('');
    end;

    local procedure GetJsonValueAsDecimal(JObject: JsonObject; PropertyName: Text): Decimal
    var
        JToken: JsonToken;
        DecVal: Decimal;
    begin
        if JObject.Get(PropertyName, JToken) then
            if JToken.IsValue() and (not JToken.AsValue().IsNull()) then
                if Evaluate(DecVal, JToken.AsValue().AsText(), 9) then
                    exit(DecVal);
        exit(0);
    end;

    local procedure GetJsonArray(JObject: JsonObject; PropertyName: Text; var JArray: JsonArray): Boolean
    var
        JToken: JsonToken;
    begin
        if JObject.Get(PropertyName, JToken) then
            if JToken.IsArray() then begin
                JArray := JToken.AsArray();
                exit(true);
            end;
        exit(false);
    end;
}
