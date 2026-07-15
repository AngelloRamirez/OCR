codeunit 56800 "OCR Utilities"
{
    Access = Public;

    [ServiceEnabled]
    procedure ProcessText(InputText: Text): Boolean
    begin
        if InputText = '' then
            exit(false);

        exit(true);
    end;
}
