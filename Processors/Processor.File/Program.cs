using Processor.File;

// Create and run the application
var app = new FileProcessorApplication();
var exitCode = await app.RunAsync(args);
Environment.ExitCode = exitCode;




