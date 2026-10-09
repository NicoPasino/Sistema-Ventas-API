namespace NicoPasino.Core.Errores
{
    public class DataException : Exception
    {
        public DataException(string message) : base(message) { }
    }

    public class UpdateException : Exception
    {
        public UpdateException(string message) : base(message) { }
    }
}
