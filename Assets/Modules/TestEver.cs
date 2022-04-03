using Debug = UnityEngine.Debug;
using UnityEngine;
using UnityEngine.UI;
using EverHelper;
using System.Threading;
using static EverHelper.EverMain;
using EverscaleNet.Client.Models;
using System.Threading.Tasks;

public class TestEver : MonoBehaviour
{
    CancellationToken stoppingToken;
    EverMain everHelper;
    string Mnemonic;
    SeGiver giver;

    public GameObject MnemonicObject;
    public GameObject GiverAddressObject;
    public GameObject MultisigAddressObject;
    public GameObject TextGiverBot;
    public GameObject TextGiverDeploed;
    public GameObject TextMultisigDeploed;

    async void Start()
    {
        TextGiverBot.active = false;
        TextGiverDeploed.active = false;
        TextMultisigDeploed.active = false;
        everHelper = new EverMain();
        await SetGiver();
        TextGiverBot.active = true;

    }
    public async Task<bool> SetGiver(CancellationToken cancellationToken = default)
    {
        Mnemonic = await everHelper.NewMnemonic(stoppingToken);
        KeyPair keys = await everHelper._everClient.Crypto.MnemonicDeriveSignKeys(
                new ParamsOfMnemonicDeriveSignKeys { Phrase = Mnemonic });
        Signer signer = new Signer.Keys { KeysAccessor = keys };
        string AddressGiver = await everHelper.DeployGiver(signer);
        giver = new SeGiver();
        giver.Address = AddressGiver;
        giver.Signer = signer;
        GiverAddressObject.transform.GetComponent<InputField>().text = AddressGiver;
        Debug.Log($"GiverAddress: {AddressGiver}");
        return true;
    }


    public async void runDeployGiver()
    {
        await everHelper.DeployGiver(giver.Signer, stoppingToken);
        TextGiverDeploed.active = true;
    }

    public async void runDeployMultisig()
    {
        if (giver.Address == "")
        {
            Debug.LogError($"Giver not set");
            return;
        }
        Mnemonic = await everHelper.NewMnemonic(stoppingToken);
        string AddressMultisig = await everHelper.DeployMultisig(Mnemonic, giver);

        MnemonicObject.transform.GetComponent<InputField>().text = Mnemonic;
        MultisigAddressObject.transform.GetComponent<InputField>().text = AddressMultisig;
        Debug.Log($"AddressMultisig: {AddressMultisig}");
        TextMultisigDeploed.active = true;
    }


}
