namespace RtkGpsBase.Enums{
    internal enum HttpParserState
    {
        METHOD,
        URL,
        URLPARM,
        URLVALUE,
        VERSION,
        HEADERKEY,
        HEADERVALUE,
        BODY,
        OK
    };
}