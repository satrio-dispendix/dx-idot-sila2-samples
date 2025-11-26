using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sila2.Dx.Idot.Sila.Dispensing.Platetraycontroller.V1;
using Sila2.Org.Silastandard;
using Sila2.Org.Silastandard.Core.Errorrecoveryservice.V2;
using SiLA2.Server.Utils;
using SiLA2.Utils.gRPC;
using System.Reflection;
using Abortprocesscontroller = Sila2.Dx.Idot.Sila.Dispensing.Abortprocesscontroller.V1;
using Barcodereaderservice = Sila2.Dx.Idot.Sila.Dispensing.Barcodereaderservice.V1;
using Boolean = Sila2.Org.Silastandard.Boolean;
using DispensingService = Sila2.Dx.Idot.Sila.Dispensing.Dispensingservice.V1;
using InitializationController = Sila2.Dx.Idot.Sila.Dispensing.Initializationcontroller.V1;
using Instrumentstatusprovider = Sila2.Dx.Idot.Sila.Dispensing.Instrumentstatusprovider.V1;
using PlateLoadingController = Sila2.Dx.Idot.Sila.Dispensing.Platetraycontroller.V1;
using ShutdownController = Sila2.Dx.Idot.Sila.Dispensing.Shutdowncontroller.V1;
using SiLAService = Sila2.Org.Silastandard.Core.Silaservice.V1;
using String = Sila2.Org.Silastandard.String;

public class ClientSample
{
    // Definition of all ávailable IDOT API client service
    private readonly DispensingService.DispensingService.DispensingServiceClient _dispensingServiceClient;
    private readonly InitializationController.InitializationController.InitializationControllerClient _initializationControllerClient;
    private readonly Abortprocesscontroller.AbortProcessController.AbortProcessControllerClient _abortProcessControllerClient;
    private readonly Barcodereaderservice.BarcodeReaderService.BarcodeReaderServiceClient _barcodeReaderServiceClient;
    private readonly Instrumentstatusprovider.InstrumentStatusProvider.InstrumentStatusProviderClient _instrumentStatusProviderClient;
    private readonly PlateLoadingController.PlateTrayController.PlateTrayControllerClient _plateTrayControllerClient;
    private readonly ShutdownController.ShutdownController.ShutdownControllerClient _shutdownControllerClient;
    private readonly ErrorRecoveryService.ErrorRecoveryServiceClient _errorRecoveryClient;
    private readonly SiLAService.SiLAService.SiLAServiceClient _siLAServiceClient;
    private static IConfigurationRoot _configuration;

    public ClientSample()
    {
        // Sample csv protocol
        string filePath = $"{AppDomain.CurrentDomain.BaseDirectory}Resources{Path.DirectorySeparatorChar}TestSila.csv";
        GrpcChannel serverChannel = FindServerChannel().Result;

        // Initialize client services
        _initializationControllerClient = new InitializationController.InitializationController.InitializationControllerClient(serverChannel);
        _dispensingServiceClient = new DispensingService.DispensingService.DispensingServiceClient(serverChannel);
        _abortProcessControllerClient = new Abortprocesscontroller.AbortProcessController.AbortProcessControllerClient(serverChannel);
        _barcodeReaderServiceClient = new Barcodereaderservice.BarcodeReaderService.BarcodeReaderServiceClient(serverChannel);
        _instrumentStatusProviderClient = new Instrumentstatusprovider.InstrumentStatusProvider.InstrumentStatusProviderClient(serverChannel);
        _plateTrayControllerClient = new PlateLoadingController.PlateTrayController.PlateTrayControllerClient(serverChannel);
        _shutdownControllerClient = new ShutdownController.ShutdownController.ShutdownControllerClient(serverChannel);
        _errorRecoveryClient = new ErrorRecoveryService.ErrorRecoveryServiceClient(serverChannel);

        _siLAServiceClient = new SiLAService.SiLAService.SiLAServiceClient(serverChannel);

        try
        {
            var serverVersion = _siLAServiceClient.Get_ServerVersion(new SiLAService.Get_ServerVersion_Parameters());
            Console.WriteLine($"Server Service Version: {serverVersion}");
        }
        catch (Exception e)
        {
            Console.WriteLine("I.DOT SiLA2 client sample can not connect to the I.DOT SiLA2 server.");
            return;
        }


    }

    public async Task RunAsync()
    {
        while (true)
        {
            Console.WriteLine("Available commands:");
            PrintAvailableCommands();

            Console.WriteLine("Enter command:");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return;

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string functionName = parts[0];
            string[] fnArgs = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            await ExecuteCommand(functionName, fnArgs);   // <-- AWAIT HERE
        }
    }

    public void PrintAvailableCommands()
    {
        var excluded = new HashSet<string>
        {
            "RunAsync",
            "PrintAvailableCommands",
            "ExecuteCommand"
        };

        var methods = this.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !excluded.Contains(m.Name));

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            string paramList = string.Join(", ",
                parameters.Select(p => $"{p.Name}:{p.ParameterType.Name}"));

            Console.WriteLine($" - {method.Name}({paramList})");
        }
    }


    public async Task ExecuteCommand(string functionName, params string[] args)
    {
        // Get method info using reflection
        MethodInfo? method = this.GetType().GetMethod(functionName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (method == null)
        {
            Console.WriteLine($"Method '{functionName}' not found.");
            return;
        }

        // Match parameter types
        ParameterInfo[] parameters = method.GetParameters();
        object?[] convertedArgs = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            // only string parameters in this example
            convertedArgs[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
        }

        // Invoke method (handle async Task)
        var result = method.Invoke(this, convertedArgs);

        if (result is Task task)
            await task;
    }

    /// <summary>
    /// This function is responsible for finding the server. By calling SearchForServers function it tries to discover the server first,
    /// and if a server doesn’t detect will try to connect to the server with the IP and port by calling GetChannel SiLA 2 function.
    /// </summary>
    /// <returns></returns>
    internal async Task<GrpcChannel> FindServerChannel()
    {
        GrpcChannel serverChannel;

        IConfigurationBuilder? configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        

        Console.WriteLine("Enter server address:");
        string? address = Console.ReadLine();
        var overrides = new Dictionary<string, string?>
            {
                { "Connection:FQHN", address }
            };
        configBuilder.AddInMemoryCollection(overrides);

        _configuration = configBuilder.Build();

        string? fqhn = _configuration["Connection:FQHN"];
        int port = int.Parse(_configuration["Connection:Port"]);

        var clientSetup = new SiLA2.Client.Configurator(_configuration, new string[] { });
        Console.WriteLine("Starting Server Discovery...");

        var serverMap = await clientSetup.SearchForServers();

        var serverType = "IDot SiLA2 Server";
        var server = serverMap.Values.FirstOrDefault(x => x.ServerType == serverType);
        if (server != null)
        {
            Console.WriteLine($"Connecting to {server}");
            serverChannel = await clientSetup.GetChannel(server.Address, server.Port, acceptAnyServerCertificate: true);
        }
        else
        {
            Console.WriteLine($"No connection automatically discovered. Using Server-URI '{fqhn}:{port}' from appSettings.config");
            serverChannel = await clientSetup.ServiceProvider.GetService<IGrpcChannelProvider>()?.GetChannel(fqhn, port, true)!;
        }

        return serverChannel;
    }

    /// <summary>
    /// This is a helper function to query and wait for a execution command
    /// </summary>
    /// <param name="executionInfo">main executed command stream info output</param>
    /// <returns></returns>
    protected async Task WaitForExecutionCommend(AsyncServerStreamingCall<ExecutionInfo> executionInfo)
    {
        IAsyncStreamReader<ExecutionInfo>? responseStream = executionInfo.ResponseStream;
        while (await responseStream.MoveNext(new CancellationToken()))
        {
            if (responseStream.Current.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedSuccessfully ||
                responseStream.Current.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedWithError)
            {
                break;
            }
        }
    }

    /// <summary>
    /// The IDOT device must be restarted after connecting API to the service and initialize the IDOT device to prepare it for executing a protocol 
    /// </summary>
    /// <param name="simulationMod">Switch server to the simulation mod</param>
    /// <returns></returns>
    public async Task InitIDotDevice(bool simulationMod = false)
    {
        try
        {
            CommandConfirmation? commandReset =
                _initializationControllerClient.Reset(new InitializationController.Reset_Parameters
                    { SimulationMode = new Boolean { Value = simulationMod } });

            using (AsyncServerStreamingCall<ExecutionInfo>? call =
                   _initializationControllerClient.Reset_Info(commandReset.CommandExecutionUUID))
            {
                await WaitForExecutionCommend(call);
                _initializationControllerClient.Reset_Result(commandReset.CommandExecutionUUID);
            }

            CommandConfirmation? commandInitialize =
                _initializationControllerClient.Initialize(new InitializationController.Initialize_Parameters());
            using (AsyncServerStreamingCall<ExecutionInfo>? call =
                   _initializationControllerClient.Initialize_Info(commandInitialize.CommandExecutionUUID))
            {
                await WaitForExecutionCommend(call);
                _initializationControllerClient.Initialize_Result(commandInitialize.CommandExecutionUUID);
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            string error = ErrorHandling.HandleException(e);
            Console.WriteLine(error);
            Console.ResetColor();
            throw;
        }
    }

    private void CheckStatus(string expectedStatus)
    {
        var instrumentStatus =
            _instrumentStatusProviderClient.Get_InstrumentStatus(new Instrumentstatusprovider.Get_InstrumentStatus_Parameters());
        var ready = false;
        while (!ready)
        {
            if (instrumentStatus.InstrumentStatus.Value == expectedStatus)
            {
                break;
            }

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"current I.DOT state {instrumentStatus.InstrumentStatus.Value}.");
            Console.WriteLine($"I.DOT to execute a protocol  should be in the {expectedStatus} state.");
            Console.ResetColor();
            Thread.Sleep(100);

            ready = true;
        }
    }

    public async Task OpenTray(string plateType)
    {
        try
        {
            CheckStatus("Idle");

            var parameters = new EjectTray_Parameters() { PlateTray = new PlateLoadingController.DataType_TrayType() { TrayType = new String() { Value = plateType } } };

            CommandConfirmation? commandEject = _plateTrayControllerClient.EjectTray(parameters);

            using (AsyncServerStreamingCall<ExecutionInfo>? call = _plateTrayControllerClient.EjectTray_Info(commandEject.CommandExecutionUUID))
            {
                await WaitForExecutionCommend(call);
                _plateTrayControllerClient.EjectTray_Result(commandEject.CommandExecutionUUID);
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            string error = ErrorHandling.HandleException(e);
            Console.WriteLine(error);
            Console.ResetColor();
            throw;
        }
    }

    public async Task CloseTray()
    {
        try
        {
            CheckStatus("Idle");

            CommandConfirmation? commandRetract = _plateTrayControllerClient.RetractTrays(new RetractTrays_Parameters());

            using (AsyncServerStreamingCall<ExecutionInfo>? call = _plateTrayControllerClient.RetractTrays_Info(commandRetract.CommandExecutionUUID))
            {
                await WaitForExecutionCommend(call);
                _plateTrayControllerClient.RetractTrays_Result(commandRetract.CommandExecutionUUID);
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            string error = ErrorHandling.HandleException(e);
            Console.WriteLine(error);
            Console.ResetColor();
            throw;
        }
    }

    /// <summary>
    /// Dispense a CSV protcol
    /// </summary>
    /// <param name="filePath">CSV protocol file path. This file should exist on the server side</param>
    public async Task DispenseProtocol(string filePath, bool monitorByPooling)
    {
        try
        {
            CheckStatus("Idle");

            //This command runs asynchronously. To query the result or get the execution status you can use the return Command Execution UUID
            CommandExecutionUUID? commandID = _dispensingServiceClient
                .DispenseProtocol(new DispensingService.DispenseProtocol_Parameters()
                {
                    FileNamePath = new Sila2.Org.Silastandard.String() { Value = filePath }
                })
                .CommandExecutionUUID;

            // Wait for command execution to finish if not for series of back to back dispensing
            if (monitorByPooling)
            {
                // wait for the server to execute first
                Console.Write("wait for the server to execute first");
                Thread.Sleep(5000);

                bool dispensingstatus = true;
                while (dispensingstatus)
                {
                    dispensingstatus = _dispensingServiceClient
                        .Get_DispensingStatus(new DispensingService.Get_DispensingStatus_Parameters()).DispensingStatus
                        .Value;
                    Thread.Sleep(200);
                }

                CheckStatus("Busy");
            }
            else
            {
                using (AsyncServerStreamingCall<ExecutionInfo>? call = _dispensingServiceClient.DispenseProtocol_Info(commandID))
                {
                    IAsyncStreamReader<ExecutionInfo>? responseStream = call.ResponseStream;
                    var cancellationToken = new CancellationTokenSource();

                    while (await responseStream.MoveNext(cancellationToken.Token))
                    {
                        // Query the dispense progress status and display it in the console
                        ExecutionInfo? currentExecutionInfo = responseStream.Current;
                        string? message =
                            $"--> Command DispenseProtocol    -status: {currentExecutionInfo.CommandStatus}   -remaining time: {currentExecutionInfo.EstimatedRemainingTime?.Seconds,3:###}s    -progress: {currentExecutionInfo.ProgressInfo.Value}";
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine(message);
                        Console.ResetColor();

                        if (currentExecutionInfo.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedSuccessfully ||
                            currentExecutionInfo.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedWithError)
                        {
                            break;
                        }
                    }
                }
            }

            var result = _dispensingServiceClient.DispenseProtocol_Result(commandID);
            if (result != null)
            {
                Console.WriteLine(result.DispenseProtocolResult.Value);
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            string error = ErrorHandling.HandleException(e);
            Console.WriteLine(error);
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Prints Drop Detection result to the console
    /// </summary>
    public void DropDetectionResult()
    {
        try
        {
            var result = _dispensingServiceClient.DropDetectionResult(new DispensingService.DropDetectionResult_Parameters());
            var misedDrops = result.DropDetectionResults
                .Where(res => res.DropDetectionResult.DetectedDropCount.Value != res.DropDetectionResult.TargetDropCount.Value)
                .ToList();
            if (misedDrops.Any())
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Drop Detection Result: Some drops were missed during dispensing.");
                foreach (var misedDrop in misedDrops)
                {
                    Console.WriteLine($"{misedDrop.DropDetectionResult.SourceWell.Value} => {misedDrop.DropDetectionResult.TargetWell.Value}: {misedDrop.DropDetectionResult.DetectedDropCount.Value} / {misedDrop.DropDetectionResult.TargetDropCount.Value} drops detected.");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("Drop Detection Result: No missed drops detected.");
            }

            Console.ResetColor();
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            string error = ErrorHandling.HandleException(e);
            Console.WriteLine(error);
            Console.ResetColor();
        }
    }

    public async Task SetFillVolume(string xmlSchema)
    {
        try
        {
            var instrumentStatus =
                _instrumentStatusProviderClient.Get_InstrumentStatus(new Instrumentstatusprovider.Get_InstrumentStatus_Parameters());
            if (instrumentStatus.InstrumentStatus.Value != "Idle")
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("I.DOT to execute a protocol  should be in the Idle state.");
                Console.ResetColor();
                return;
            }

            //This command runs asynchronously. To query the result or get the execution status you can use the return Command Execution UUID
            CommandExecutionUUID? commandID = _dispensingServiceClient
                .SetFillVolume(new DispensingService.SetFillVolume_Parameters()
                {
                    FillVolumes = new Sila2.Org.Silastandard.String() { Value = xmlSchema }
                })
                .CommandExecutionUUID;

            // Wait for command execution to finish
            using (AsyncServerStreamingCall<ExecutionInfo>? call = _dispensingServiceClient.SetFillVolume_Info(commandID))
            {
                IAsyncStreamReader<ExecutionInfo>? responseStream = call.ResponseStream;
                var cancellationToken = new CancellationTokenSource();

                while (await responseStream.MoveNext(cancellationToken.Token))
                {
                    // Query the dispense progress status and display it in the console
                    ExecutionInfo? currentExecutionInfo = responseStream.Current;
                    string? message =
                        $"--> Command DispenseProtocol    -status: {currentExecutionInfo.CommandStatus}   -remaining time: {currentExecutionInfo.EstimatedRemainingTime?.Seconds,3:###}s    -progress: {currentExecutionInfo.ProgressInfo.Value}";
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.WriteLine(message);
                    Console.ResetColor();

                    if (currentExecutionInfo.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedSuccessfully ||
                        currentExecutionInfo.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedWithError)
                    {
                        break;
                    }
                }
            }

            _dispensingServiceClient.SetFillVolume_Result(commandID);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            string error = ErrorHandling.HandleException(e);
            Console.WriteLine(error);
            Console.ResetColor();
        }
    }

    public async Task TransferLiquid(string dispenseXmlSchema, bool optimizeDispenseStepOrder)
    {
        try
        {
            var instrumentStatus =
                _instrumentStatusProviderClient.Get_InstrumentStatus(new Instrumentstatusprovider.Get_InstrumentStatus_Parameters());
            if (instrumentStatus.InstrumentStatus.Value != "Idle")
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("I.DOT to execute a protocol  should be in the Idle state.");
                Console.ResetColor();
                return;
            }

            //This command runs asynchronously. To query the result or get the execution status you can use the return Command Execution UUID
            CommandExecutionUUID? commandID = _dispensingServiceClient
                .TransferLiquid(new DispensingService.TransferLiquid_Parameters()
                {
                    //FileNamePath = new Sila2.Org.Silastandard.String() { Value = filePath }
                    DispenseStepXmlSchema = new String() { Value = dispenseXmlSchema },
                    OptimizeDispenseStepOrder = new Boolean() { Value = optimizeDispenseStepOrder }
                })
                .CommandExecutionUUID;

            // Wait for command execution to finish
            using (AsyncServerStreamingCall<ExecutionInfo>? call = _dispensingServiceClient.TransferLiquid_Info(commandID))
            {
                IAsyncStreamReader<ExecutionInfo>? responseStream = call.ResponseStream;
                var cancellationToken = new CancellationTokenSource();

                while (await responseStream.MoveNext(cancellationToken.Token))
                {
                    // Query the dispense progress status and display it in the console
                    ExecutionInfo? currentExecutionInfo = responseStream.Current;
                    string? message =
                        $"--> Command TransferLiquid   -status: {currentExecutionInfo.CommandStatus}   -remaining time: {currentExecutionInfo.EstimatedRemainingTime?.Seconds,3:###}s    -progress: {currentExecutionInfo.ProgressInfo.Value}";
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.WriteLine(message);
                    Console.ResetColor();

                    if (currentExecutionInfo.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedSuccessfully ||
                        currentExecutionInfo.CommandStatus == ExecutionInfo.Types.CommandStatus.FinishedWithError)
                    {
                        break;
                    }
                }
            }

            DispensingService.TransferLiquid_Responses? response = _dispensingServiceClient.TransferLiquid_Result(commandID);
            Console.WriteLine(response.TransferLiquidResult.Value);
            Console.WriteLine("\n");
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            string error = ErrorHandling.HandleException(e);
            Console.WriteLine(error);
            Console.ResetColor();
            Console.WriteLine("\n");
        }
    }
}