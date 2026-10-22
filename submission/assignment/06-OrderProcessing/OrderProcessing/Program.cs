using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Storage;

Log.Info("hello from info");
Log.Success("hello from success");
Log.Error("hello from error");

await DataSeeder.ResetAsync();
Console.WriteLine("Data folder: " + DataFiles.Folder);