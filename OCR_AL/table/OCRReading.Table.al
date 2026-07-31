table 56800 "OCR Reading"
{
    Caption = 'OCR Reading';
    DataClassification = ToBeClassified;

    fields
    {
        field(1; "Entry No"; Integer)
        {
            Caption = 'Entry No';
        }
        field(2; "OCR JSON Reading"; Blob)
        {
            Caption = 'OCR JSON Reading';
        }
        field(3; "Document Created"; Boolean)
        {
            Caption = 'Document Created';
        }
        field(4; "Document No."; Code[20])
        {
            Caption = 'Document No.';
        }
        field(5; "Document Type"; Enum "Purchase Document Type")
        {
            Caption = 'Document Type';
        }
        field(6; "File Name"; Text[200])
        {
            Caption = 'File Name';
        }
    }
    keys
    {
        key(PK; "Entry No")
        {
            Clustered = true;
        }
    }


    procedure GetRequestText(): Text
    var
        InS: InStream;
        Txt: Text;
    begin
        CalcFields("OCR JSON Reading");
        if "OCR JSON Reading".HasValue then begin
            "OCR JSON Reading".CreateInStream(InS, TEXTENCODING::UTF8);
            InS.ReadText(Txt);
        end;
        exit(Txt);
    end;

}
