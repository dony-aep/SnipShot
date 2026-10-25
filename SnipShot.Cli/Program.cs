namespace SnipShot.Cli
{
    /// <summary>
    /// Punto de entrada del comando <c>snipshot</c>. Escribe siempre JSON en la salida
    /// estándar para que lo pueda leer un agente o un script.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "status")
            {
                // Todavía no hay canal con la app: la fase 1 del plan añade el pipe.
                Console.WriteLine("{\"app\":\"unknown\"}");
                return 0;
            }

            Console.Error.WriteLine("Uso: snipshot status");
            return 2;
        }
    }
}
