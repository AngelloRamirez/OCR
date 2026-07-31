codeunit 56800 "OCR Utilities"
{
    Access = Public;
    Permissions = tabledata "OCR Reading" = rimd;

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
}
