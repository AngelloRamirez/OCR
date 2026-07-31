page 56800 "OCR Readings List"
{
    PageType = List;
    ApplicationArea = All;
    UsageCategory = Lists;
    SourceTable = "OCR Reading";
    InsertAllowed = false;

    layout
    {
        area(Content)
        {
            repeater(GroupName)
            {
                field("Entry No"; Rec."Entry No")
                {
                    ApplicationArea = All;
                }
                field("Document Created"; Rec."Document Created")
                {
                    ApplicationArea = All;
                }
                field("Document No."; Rec."Document No.")
                {
                    ApplicationArea = All;
                }
                field("Document Type"; Rec."Document Type")
                {
                    ApplicationArea = All;
                }
                field("File Name"; Rec."File Name")
                {
                    ApplicationArea = All;
                }
            }
        }
    }

    actions
    {
        area(Processing)
        {
            action(seeReading)
            {
                ApplicationArea = All;
                Caption = 'See Reading';
                Image = ViewDetails;

                trigger OnAction()
                var
                    OCRTextPage: Page "OCR JSON";
                begin
                    OCRTextPage.SetText(Rec.GetRequestText());
                    OCRTextPage.RunModal()
                end;
            }
            action(CreateDocument)
            {
                ApplicationArea = All;
                Caption = 'Create Document';
                Image = Add;
                trigger OnAction()
                begin

                end;
            }
        }
        area(Promoted)
        {
            actionref(CreateDocument_promoted; CreateDocument) { }
        }
    }
}