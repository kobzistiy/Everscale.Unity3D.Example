# Everscale.Unity3D.Example


The Everscale .NET Client release was taken as the basis (https://github.com/everscale-actions/everscale-dotnet) The release is in line with Unity restrictions Implemented demo functionality for deploying contacts and working with them. In the next releases, it is planned to implement work with native tokens and NFTs

# Usage

Install it through OpenUPM or use the Unitypackage from the Releases.

```bash
```

```c#
    public IEverClient CreateClient()
    {
        var options = new EverClientOptions
        {
            Network = new NetworkConfig
            {
                Endpoints = new[] {
                    "https://eri01.net.everos.dev/",
                    "https://rbx01.net.everos.dev/",
                    "https://gra01.net.everos.dev/"
                }
            }
        };
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(
            new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Unity3D()
            .CreateLogger()
        ));
        ILogger<EverClientRustAdapter> _logger = loggerFactory.CreateLogger<EverClientRustAdapter>();

        var adapter = new EverClientRustAdapter(new OptionsWrapper<EverClientOptions>(options), _logger);

        return new EverClient(adapter);
    }
```

## Version from 1.0.0

- Deploy Giver smart contract
- Take Rubins from Giver to new address
- Deploy multisig smart contract
