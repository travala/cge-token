using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class CGEToken : Framework.SmartContract
    {
        //Token Settings
        public static string Name() => "Concierge Token";
        public static string Symbol() => "CGE";

        // The owner address.
        // TODO: Update this part when deploying to main net.
        public static readonly byte[] Owner = { 185, 129, 156, 44, 117, 69, 46, 55, 142, 209, 188, 180, 56, 146, 2, 165, 108, 188, 129, 122 };
        public static byte Decimals() => 8;
        private const ulong factor = 100000000; //decided by Decimals()
        private const ulong neo_decimals = 100000000;

        //ICO Settings
        private static readonly byte[] NEO_ASSET_ID = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private const ulong TOTAL_AMOUNT = 100000000 * factor;      // Maximum amount of tokens that can be available in the chain
        private const ulong OWNER_AMOUNT = 35000000 * factor;       // The portion that owner will hold.

        // ICO phases
        // TODO: make configurable settings?
        private const int PRE_SALE_BEGIN_AT = 1515229200;           // Saturday, January 6, 2018 9:00:00 AM
        private const int PRE_SALE_END_AT = 1517648400;             // Saturday, February 3, 2018 9:00:00
        private const int MAIN_SALE_BEGIN_AT = 1517907600;          // Tuesday, February 6, 2018 9:00:00 AM
        private const int MAIN_SALE_END_AT = 1520326800;            // Tuesday, March 6, 2018 9:00:00 AM

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> Refund;

        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (Owner.Length == 20)
                {
                    // if param Owner is script hash
                    return Runtime.CheckWitness(Owner);
                }
                else if (Owner.Length == 33)
                {
                    // if param Owner is public key
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, Owner);
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "deploy") return Deploy();
                if (operation == "mintTokens") return MintTokens();
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (operation == "decimals") return Decimals();
            }

            // TODO: do we refund in case of wrong transfer?
            byte[] sender = GetSender();
            ulong contribute_value = GetContributeValue();
            if (contribute_value > 0 && sender.Length != 0)
            {
                Refund(sender, contribute_value);
            }

            return false;
        }

        // initialization parameters, run only once
        public static bool Deploy()
        {
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            if (total_supply.Length != 0) return false;
            Storage.Put(Storage.CurrentContext, Owner, OWNER_AMOUNT);
            Storage.Put(Storage.CurrentContext, "totalSupply", OWNER_AMOUNT);
            Transferred(null, Owner, OWNER_AMOUNT);
            return true;
        }

        // The function MintTokens is only usable by the chosen wallet
        // contract to mint a number of tokens proportional to the
        // amount of neo sent to the wallet contract. The function
        // can only be called during the tokenswap period
        public static bool MintTokens()
        {
            byte[] sender = GetSender();
            // If the asset is not NEO, just ignore
            if (sender.Length == 0)
            {
                return false;
            }

            // The contribution amount was sent.
            ulong contribute_value = GetContributeValue();
            // Get current exchange rate
            ulong swap_rate = CurrentSwapRate();

            // Refund if now is not in sale phase, or all tokens were sold out.
            if (swap_rate == 0)
            {
                Refund(sender, contribute_value);
                return false;
            }

            // You got tokens
            ulong token = CurrentSwapToken(sender, contribute_value, swap_rate);
            if (token == 0)
            {
                return false;
            }

            // Update balances
            BigInteger balance = Storage.Get(Storage.CurrentContext, sender).AsBigInteger();
            Storage.Put(Storage.CurrentContext, sender, token + balance);
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalSupply", token + totalSupply);
            Transferred(null, sender, token);
            return true;
        }

        // Current total supplied tokens.
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        // Function is called when someone wants to transfer tokens.
        // TODO: prevent tranferring before ICO is finished?
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            if (from == to) return true;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return false;
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_value + value);
            Transferred(from, to, value);
            return true;
        }

        // Get the balance of a address
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        // Get current exchange rate between tokens and NEO in sale phase
        private static ulong CurrentSwapRate()
        {
            int now = (int) (Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp + 15);
            int WEEK_IN_SECONS = 604800;

            // In pre-sale
            if (now > PRE_SALE_BEGIN_AT && now < PRE_SALE_END_AT)
            {
                // The first week
                if (now < PRE_SALE_BEGIN_AT + WEEK_IN_SECONS)
                {
                    return 311 * factor;
                }
                else if (now < PRE_SALE_BEGIN_AT + 2 * WEEK_IN_SECONS)
                {
                    return 274 * factor;
                }
                else if (now < PRE_SALE_BEGIN_AT + 3 * WEEK_IN_SECONS)
                {
                    return 259 * factor;
                }
                else
                {
                    return 233 * factor;
                }
            }
            // In main-sale
            else if (now > MAIN_SALE_BEGIN_AT && now < MAIN_SALE_END_AT)
            {
                if (now < MAIN_SALE_BEGIN_AT + WEEK_IN_SECONS)
                {
                    return 187 * factor;
                }
                else if (now < MAIN_SALE_BEGIN_AT + 2 * WEEK_IN_SECONS)
                {
                    return 165 * factor;
                }
                else if (now < MAIN_SALE_BEGIN_AT + 3 * WEEK_IN_SECONS)
                {
                    return 155 * factor;
                }
                else if (now < MAIN_SALE_BEGIN_AT + 3 * WEEK_IN_SECONS)
                {
                    return 140 * factor;
                }
            }

            // Else nothing for you.
            return 0;
        }

        // Get total valid exchange tokens
        private static ulong CurrentSwapToken(byte[] sender, ulong value, ulong swap_rate)
        {
            // Amount that user wants to buy
            ulong token = value / neo_decimals * swap_rate;

            // Current total supply tokens.
            BigInteger total_supply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();

            // The available tokens left.
            BigInteger balance_token = TOTAL_AMOUNT - total_supply;

            // If there's no available token left, just refund
            if (balance_token <= 0)
            {
                Refund(sender, value);
                return 0;
            }
            // If available tokens is not enough, just exchange available ones, and refund the rest to user
            else if (balance_token < token)
            {
                Refund(sender, (token - balance_token) / swap_rate * neo_decimals);
                token = (ulong)balance_token;
            }

            return token;
        }

        // Check whether asset is neo and get sender script hash
        private static byte[] GetSender()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] reference = tx.GetReferences();
            // you can choice refund or not refund
            foreach (TransactionOutput output in reference)
            {
                if (output.AssetId == NEO_ASSET_ID) return output.ScriptHash;
            }

            return new byte[0];
        }

        // Get smart contract script hash
        private static byte[] GetReceiver()
        {
            return ExecutionEngine.ExecutingScriptHash;
        }

        // Get all you contribute neo amount
        private static ulong GetContributeValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;

            // get the total amount of Neo
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == GetReceiver() && output.AssetId == NEO_ASSET_ID)
                {
                    value += (ulong)output.Value;
                }
            }

            return value;
        }
    }
}
