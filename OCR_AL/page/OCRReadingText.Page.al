page 56801 "OCR JSON"
{
    PageType = Card;
    ApplicationArea = All;

    layout
    {
        area(Content)
        {
            group(General)
            {
                ShowCaption = false;
                field(OCRText; OCRText)
                {
                    ApplicationArea = All;
                    Editable = false;
                    MultiLine = true;
                    ShowCaption = false;
                    ExtendedDatatype = RichContent;
                }
            }
        }
    }

    trigger OnOpenPage()
    var
        JsonMngmt: Codeunit "Json Library";
    begin
        OCRText := DelChr(OCRText, '<>', '[');
        OCRText := DelChr(OCRText, '<>', ']');
        OCRText := JsonMngmt.PrettyPrintJsonContent(OCRText);
    end;

    procedure SetText(InputText: Text)
    begin
        OCRText := InputText;
    end;

    var
        OCRText: Text;
}