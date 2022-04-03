using Debug = UnityEngine.Debug;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EverscaleNet.Abstract;
using EverscaleNet.Client.Models;
using EverscaleNet.Models;
using EverscaleNet.Serialization;
using EverscaleNet.Utils;
using EverscaleNet.Client.PackageManager;
using EverscaleNet.Adapter.Rust;
using EverscaleNet.Client;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.Unity3D;

namespace EverHelper {

public class EverMain
    {
        private const int DefaultAbiVersion = 2;
        private const string ContractsPath = "Assets/Plugins/_contracts";
        public IEverClient _everClient;
        private static IEverPackageManager _packageManager;
        public EverMain()
        {
            _everClient = CreateClient();
            _packageManager = new FilePackageManager(
                Options.Create(
                    new FilePackageManagerOptions{ PackagesPath = System.IO.Path.Combine(ContractsPath, $"abi_v{DefaultAbiVersion}") }
                )
             );
        }
        public class SeGiver
        {
            public string Address;
            public Signer Signer;
        }

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
        public async Task<string> NewMnemonic(CancellationToken cancellationToken = default)
        {
            ResultOfMnemonicFromRandom ResultOfMnemonic = await _everClient.Crypto.MnemonicFromRandom(new ParamsOfMnemonicFromRandom());
            string Mnemonic = ResultOfMnemonic.Phrase;
            Debug.Log($"New Mnemonic: {Mnemonic}");

            return Mnemonic;
        }


        public async Task<decimal> GetBalance(string account, CancellationToken cancellationToken = default)
        {
            ResultOfQueryCollection result = await _everClient.Net.QueryCollection(new ParamsOfQueryCollection
            {
                Collection = "accounts",
                Filter = new { id = new { eq = account } }.ToJsonElement(),
                Result = "balance(format: DEC)",
                Limit = 1
            });
            decimal balance = 0;
            if (result.Result.Length != 0)
            {
                balance = result.Result[0].Get<string>("balance").DecBalanceToCoins();
            }
            Debug.Log($"balance: {balance}");

            return balance;
        }

        public async Task<string> DeployGiver(Signer signer, CancellationToken cancellationToken = default)
        {
            Package giverContract = await _packageManager.LoadPackage("GiverV2");

            var deployParams = new ParamsOfEncodeMessage
            {
                Abi = giverContract.Abi,
                DeploySet = new DeploySet { Tvc = giverContract.Tvc },
                Signer = signer,
                CallSet = new CallSet { FunctionName = "constructor" }
            };

            ResultOfEncodeMessage encoded = await _everClient.Abi.EncodeMessage(deployParams);
            Debug.Log($"Address: {encoded.Address}");

            decimal balance = await GetBalance(encoded.Address);

            if (balance < 10m)
            {
                Debug.LogError("Balance < 10m");
            }
            else
            {

                try
                {
                    await ProcessAndWaitTransactions(deployParams);
                }
                catch (EverClientException e) when (e.Code == 414)
                {
                    Debug.Log("Contract already has been deployed");
                }
            }
            return encoded.Address;
        }

        public async Task<string> DeployMultisig(string Mnemonic, SeGiver giver, CancellationToken cancellationToken = default)
        {
            Package contract = await _packageManager.LoadPackage("SafeMultisigWallet");

            // get keys by mnemonic
            KeyPair keys = await _everClient.Crypto.MnemonicDeriveSignKeys(
                    new ParamsOfMnemonicDeriveSignKeys { Phrase = Mnemonic });

            string contractAddress = await CheckBalanceAndDeploy(contract, keys, giver);
            return contractAddress;

        }

        public async Task<string> CheckBalanceAndDeploy(Package package, KeyPair keys, SeGiver giver, CancellationToken cancellationToken = default)
        {
            var deployParams = new ParamsOfEncodeMessage
            {
                Abi = package.Abi,
                DeploySet = new DeploySet { Tvc = package.Tvc },
                Signer = new Signer.Keys { KeysAccessor = keys },
                CallSet = new CallSet { 
                    FunctionName = "constructor",
                    Input = new
                    {
                        owners = new string[] { "0x" + keys.Public },
                        reqConfirms = 1
                    }.ToJsonElement()
                }
            };

            ResultOfEncodeMessage encoded = await _everClient.Abi.EncodeMessage(deployParams);
            Debug.Log($"Address: {encoded.Address}");

            decimal balance = await GetBalance(encoded.Address);

            if (balance < 10m)
            {
                await SendGramsFromGiver(encoded.Address, giver);
            }

            try
            {
                await ProcessAndWaitTransactions(deployParams);
            }
            catch (EverClientException e) when (e.Code == 414)
            {
                Debug.Log("Contract already has been deployed");
            }

            return encoded.Address;
        }

        public async Task SendGramsFromGiver(string account, SeGiver giver, CancellationToken cancellationToken = default) {

            var sendGramsEncodedMessage = new ParamsOfEncodeMessage {
                Address = giver.Address,
                Abi = await _packageManager.LoadAbi("GiverV2"),
                CallSet = new CallSet {
                    FunctionName = "sendTransaction",
                    Input = new {
                        dest = account,
                        value = 1_000_000_000,
                        bounce = false
                    }.ToJsonElement()
                },
                Signer = giver.Signer
            };
            Debug.Log($"sendGramsEncodedMessage: {sendGramsEncodedMessage.ToJsonElement()}");
            await ProcessAndWaitTransactions(sendGramsEncodedMessage);
            Debug.Log($"sendGramsEncodedMessage: OK");
        }

        public async Task ProcessAndWaitTransactions(ParamsOfEncodeMessage encodedMessage,
                                                      CancellationToken cancellationToken = default) {
            ResultOfProcessMessage resultOfProcessMessage = await _everClient.Processing.ProcessMessage(
                                                                new ParamsOfProcessMessage {
                                                                    MessageEncodeParams = encodedMessage
                                                                }, cancellationToken: cancellationToken);

            await Task.WhenAll(resultOfProcessMessage.OutMessages.Select(async message => {
                ResultOfParse parseResult =
                    await _everClient.Boc.ParseMessage(new ParamsOfParse { Boc = message });
                var parsedPrototype = new { type = default(int), id = default(string) };
                var parsedMessage = parseResult.Parsed!.Value.ToAnonymous(parsedPrototype);
                if (parsedMessage.type == 0) {
                    await _everClient.Net.WaitForCollection(new ParamsOfWaitForCollection {
                        Collection = "transactions",
                        Filter = new { in_msg = new { eq = parsedMessage.id } }.ToJsonElement(),
                        Result = "id"
                    });
                }
            }));
        }

    }
}
