static class Logger
{
    public static void log(String message)
    {
        Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}] {message}");
    }
}
