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
                field("Error Message"; Rec."Error Message")
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
                var
                    OCRUtilities: Codeunit "OCR Utilities";
                begin
                    OCRUtilities.CreatePurchaseInvoiceFromJson(Rec, Rec.GetRequestText());
                end;
            }
            action(test_processText)
            {
                applicationArea = All;
                Caption = 'Test Process Text';
                Image = Process;
                visible = false;
                trigger OnAction()
                var
                begin
                    Report.Run(56800);
                end;
            }
        }
        area(Promoted)
        {
            actionref(CreateDocument_promoted; CreateDocument) { }
        }
    }
}


report 56800 Parameter
{
    UsageCategory = ReportsAndAnalysis;
    ApplicationArea = All;
    ProcessingOnly = true;

    requestpage
    {
        AboutTitle = 'Teaching tip title';
        AboutText = 'Teaching tip content';
        layout
        {
            area(Content)
            {
                group(GroupName)
                {
                    field(inputtext; inputtext)
                    {
                        ApplicationArea = All;
                    }
                    field(filename; filename)
                    {
                        ApplicationArea = All;
                    }
                }
            }
        }
    }

    trigger OnInitReport()
    begin
        inputtext := 'Sample input text';
        filename := 'sample_file.txt';
    end;

    trigger OnPostReport()
    var
        OCRUtilities: Codeunit "OCR Utilities";
    begin
        OCRUtilities.ProcessText(inputtext, filename);
        Message('Done');
    end;

    var
        inputtext: Text;
        filename: Text;
}