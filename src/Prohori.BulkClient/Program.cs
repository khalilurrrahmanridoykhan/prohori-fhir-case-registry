using Prohori.BulkClient;

if (args is ["--keygen", var directory])
{
    BackendKey.Generate(directory);
    Console.WriteLine($"Generated local backend key and public JWKS in {directory}.");
}
else Console.WriteLine("Use --keygen DIRECTORY to provision the backend-service key.");
