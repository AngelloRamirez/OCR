codeunit 56801 "Json Library"
{
    procedure PrettyPrintJsonContent(JsonContent: Text): Text
    var
        jObject: JsonObject;
    begin
        jObject.ReadFrom(JsonContent);
        exit(PrettyPrintJsonContent(jObject));
    end;

    procedure PrettyPrintJsonContent(jObject: JsonObject): Text
    begin
        exit(DoPrettyPrintJsonContent(jObject, 0));
    end;

    local procedure DoPrettyPrintJsonContent(jObject: JsonObject; Indent: Integer): Text;
    var
        tb: TextBuilder;
    begin
        tb.AppendLine(GetIndent(Indent) + '{');

        tb.Append(FormatJsonContent(jObject, Indent));

        tb.AppendLine(GetIndent(Indent) + '}');

        exit(tb.ToText());
    end;

    local procedure FormatJsonContent(jObject: JsonObject; var Indent: Integer): Text;
    var
        ValueContent: Text;
        Counter: Integer;
        i: Integer;
        tb: TextBuilder;
        jArray: JsonArray;
        jToken: JsonToken;
        ValuePair: Label '"%1":"%2"', Locked = true;
    begin
        Indent += 1;

        foreach jToken in jObject.Values do begin
            case (true) of
                jToken.IsArray:
                    begin
                        jArray := jToken.AsArray();
                        tb.AppendLine(GetIndent(Indent) + '"' + GetTokenName(jToken) + '"' + ':' + '[');
                        for i := 0 to (jArray.Count - 1) do begin
                            jArray.Get(i, jToken);
                            tb.Append(DoPrettyPrintJsonContent(jToken.AsObject(), Indent + 1));
                        end;
                        tb.AppendLine(GetIndent(Indent) + ']');
                    end;
                jToken.IsObject:
                    begin
                        tb.AppendLine(GetIndent(Indent) + '"' + GetTokenName(jToken) + '"' + ':');
                        tb.Append(DoPrettyPrintJsonContent(jToken.AsObject(), Indent));
                    end;
                jToken.IsValue:
                    begin
                        Clear(ValueContent);
                        if (not jToken.AsValue().IsNull) and (not jToken.AsValue().IsUndefined) then
                            ValueContent := jToken.AsValue().AsText();
                        ValueContent := GetIndent(Indent) + StrSubstNo(ValuePair, GetTokenName(jToken), ValueContent);
                        if (Counter < (jObject.Values.Count - 1)) then
                            ValueContent += ',';
                        tb.AppendLine(ValueContent);
                    end;
            end;

            Counter += 1;
        end;

        Indent -= 1;

        exit(tb.ToText());
    end;

    procedure GetTokenName(jToken: JsonToken) Output: Text
    begin
        Output := jToken.Path;

        while (StrPos(Output, '.') > 0) do
            Output := CopyStr(Output, StrPos(Output, '.') + 1);

        exit(Output);
    end;

    local procedure GetIndent(Count: Integer) IndentValue: Text
    var
        Spacer: Char;
        i: Integer;
    begin
        Spacer := 12288;
        for i := 1 to Count do
            IndentValue += Spacer;
    end;
}