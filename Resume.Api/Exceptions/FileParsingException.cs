namespace Resume.Api.Exceptions;

public class FileParsingException(string message, Exception? innerException = null)
    : Exception(message, innerException);
