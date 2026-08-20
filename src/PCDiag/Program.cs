using PCDiag.CLI;
using PCDiag.Interactive;

if (args.Length > 0 && args[0].Equals("info", StringComparison.OrdinalIgnoreCase))
{
    return await InventoryCommand.RunAsync();
}

if (args.Length >= 2 && args[0].Equals("check", StringComparison.OrdinalIgnoreCase))
{
    var commandArgs = args.Skip(2).ToArray();
    return args[1].ToLowerInvariant() switch
    {
        "dns" => await DnsCommand.RunAsync(),
        "mtu" => await MtuCommand.RunAsync(commandArgs),
        "gateway" => await GatewayCommand.RunAsync(commandArgs),
        "packet-loss" => await PacketLossCommand.RunAsync(commandArgs),
        "tcp" => await TcpCommand.RunAsync(),
        "connections" => await ConnectionsCommand.RunAsync(),
        "events" => await EventsCommand.RunAsync(),
        "whea" => await WheaCommand.RunAsync(),
        "drivers" => await DriversCommand.RunAsync(),
        "memory" => await MemoryCommand.RunAsync(),
        "pagefile" => await PagefileCommand.RunAsync(),
        "storage" => await StorageCommand.RunAsync(),
        _ => PrintUnknownCheck(args[1])
    };
}

return await InteractiveApp.RunAsync();

static int PrintUnknownCheck(string name)
{
    Console.Error.WriteLine($"Unknown check: {name}");
    Console.Error.WriteLine("Available checks: dns, mtu, gateway, packet-loss, tcp, connections, events, whea, drivers, memory, pagefile, storage");
    return 1;
}