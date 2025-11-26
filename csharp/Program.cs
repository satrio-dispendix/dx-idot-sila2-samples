namespace IDot.SiLA2.Samples.CSharp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var client = new ClientSample();
            await client.RunAsync();
            Console.ReadKey();
        }

    }
}