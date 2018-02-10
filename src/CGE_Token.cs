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
        public static readonly byte[] Owner = "AKnA1QPaR1AEX7R1NBgBfJ5N97uZXSfbtP".ToScriptHash();
        public static readonly byte[] Operator = "AY4PQo2v4BPLgdUTBgUbaXgJ7botdQg6P2".ToScriptHash();
        public static byte Decimals() => 8;
        private const ulong factor = 100000000; //decided by Decimals()
        private const ulong neo_decimals = 100000000;

        //ICO Settings
        private static readonly byte[] NEO_ASSET_ID = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private const ulong TOTAL_AMOUNT = 100000000 * factor;      // Maximum amount of tokens that can be available in the chain
        private const ulong OWNER_AMOUNT = 39000000 * factor;       // The portion that owner will hold.
        private static readonly byte[] WHITELIST_FLAG = { 49 };

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
                if (operation == "addToWhitelist")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return AddToWhiteList(account);
                }
                if (operation == "removeFromWhitelist")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return RemoveFromWhiteList(account);
                }
                if (operation == "isInWhitelist")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return IsInWhiteList(account);
                }
                if (operation == "getParamValueInt")
                {
                    if (args.Length != 1) return 0;
                    byte[] paramName = (byte[])args[0];
                    return GetParamValueInt(paramName);
                }
                if (operation == "setWhitelistSaleBegin")
                {
                    return SetParamValueByString("WHITELIST_SALE_BEGIN", (BigInteger)args[0]);
                }
                if (operation == "setWhitelistSaleRate")
                {
                    return SetParamValueByString("WHITELIST_SALE_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setWhitelistHardcap")
                {
                    return SetParamValueByString("WHITELIST_HARD_CAP", (BigInteger)args[0]);
                }
                if (operation == "setPresaleBegin")
                {
                    return SetParamValueByString("PRE_SALE_BEGIN", (BigInteger)args[0]);
                }
                if (operation == "setPresaleWeek1Rate")
                {
                    return SetParamValueByString("PRE_SALE_WEEK1_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setPresaleWeek2Rate")
                {
                    return SetParamValueByString("PRE_SALE_WEEK2_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setPresaleWeek3Rate")
                {
                    return SetParamValueByString("PRE_SALE_WEEK3_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setPresaleWeek4Rate")
                {
                    return SetParamValueByString("PRE_SALE_WEEK4_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setPresaleHardcap")
                {
                    return SetParamValueByString("PRE_SALE_HARD_CAP", (BigInteger)args[0]);
                }
                if (operation == "setMainsaleBegin")
                {
                    return SetParamValueByString("MAIN_SALE_BEGIN", (BigInteger)args[0]);
                }
                if (operation == "setMainsaleWeek1Rate")
                {
                    return SetParamValueByString("MAIN_SALE_WEEK1_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setMainsaleWeek2Rate")
                {
                    return SetParamValueByString("MAIN_SALE_WEEK2_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setMainsaleWeek3Rate")
                {
                    return SetParamValueByString("MAIN_SALE_WEEK3_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setMainsaleWeek4Rate")
                {
                    return SetParamValueByString("MAIN_SALE_WEEK4_RATE_NEO", (BigInteger)args[0]);
                }
                if (operation == "setMainsaleHardcap")
                {
                    return SetParamValueByString("MAIN_SALE_HARD_CAP", (BigInteger)args[0]);
                }
                if (operation == "setMaxPurchase")
                {
                    return SetParamValueByString("MAX_NEO_PER_TRANSFER", (BigInteger)args[0]);
                }
            }

            // Refund in case of wrong transfer
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

            // Whitelist sale begin: Tuesday, February 13, 2018 9:00:00 AM GMT
            Storage.Put(Storage.CurrentContext, "WHITELIST_SALE_BEGIN", 1518512400);

            // Whitelist sale hardcap
            Storage.Put(Storage.CurrentContext, "WHITELIST_HARD_CAP", 45000000);

            // Pre-sale begin: Wednesday, February 14, 2018 9:00:00 AM GMT
            Storage.Put(Storage.CurrentContext, "PRE_SALE_BEGIN", 1518598800);

            // Pre-sale hardcap
            Storage.Put(Storage.CurrentContext, "PRE_SALE_HARD_CAP", 55000000);

            // Main-sale begin: Saturday, March 31, 2018 9:00:00 AM GMT
            Storage.Put(Storage.CurrentContext, "MAIN_SALE_BEGIN", 1522486800);

            // Main-sale hardcap
            Storage.Put(Storage.CurrentContext, "MAIN_SALE_HARD_CAP", 100000000);

            Storage.Put(Storage.CurrentContext, "MAX_NEO_PER_TRANSFER", 250);

            // First minted tokens are granted to owner
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
            ulong exchange_rate = GetCurrentExchangeRate();

            // Refund if now is not in sale phase, or all tokens were sold out.
            if (exchange_rate == 0)
            {
                Refund(sender, contribute_value);
                return false;
            }

            // Determine exchangeable tokens, based on current exchange rate, contributed value and number of tokens left
            ulong exchangeable_tokens = GetExchangeableTokens(sender, contribute_value, exchange_rate);

            // No more tokens can be minted
            if (exchangeable_tokens == 0)
            {
                return false;
            }

            // Else update balance and total supply
            BigInteger balance = Storage.Get(Storage.CurrentContext, sender).AsBigInteger();
            Storage.Put(Storage.CurrentContext, sender, exchangeable_tokens + balance);
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalSupply", exchangeable_tokens + totalSupply);
            Transferred(null, sender, exchangeable_tokens);
            return true;
        }

        // Current total supplied tokens.
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        // Function is called when someone wants to transfer tokens.
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

        // Get the value of a parameter
        public static BigInteger GetParamValueInt(byte[] paramName)
        {
            return Storage.Get(Storage.CurrentContext, paramName).AsBigInteger();
        }

        // Set the value of a parameter
        public static Boolean SetParamValueInt(byte[] paramName, BigInteger paramValue)
        {
            // Only owner or operator can update the value of parameters
            if (!Runtime.CheckWitness(Owner) && !Runtime.CheckWitness(Operator))
            {
                return false;
            }

            Storage.Put(Storage.CurrentContext, paramName, paramValue);
            return true;
        }

        public static Boolean SetParamValueByString(String paramName, BigInteger paramValue)
        {
            // Only owner or operator can update the value of parameters
            if (!Runtime.CheckWitness(Owner) && !Runtime.CheckWitness(Operator))
            {
                return false;
            }

            Storage.Put(Storage.CurrentContext, paramName, paramValue);
            return true;
        }

        public static Boolean AddToWhiteList(byte[] address)
        {
            byte[] paramName = address.Concat(WHITELIST_FLAG);
            return SetParamValueInt(paramName, 1);
        }

        public static Boolean RemoveFromWhiteList(byte[] address)
        {
            byte[] paramName = address.Concat(WHITELIST_FLAG);
            return SetParamValueInt(paramName, 0);
        }

        public static BigInteger IsInWhiteList(byte[] address)
        {
            byte[] paramName = address.Concat(WHITELIST_FLAG);
            return GetParamValueInt(paramName);
        }

        // Get current exchange rate between tokens and NEO in sale phase
        private static ulong GetCurrentExchangeRate()
        {
            ulong now = Runtime.Time;
            ulong WEEK_IN_SECONS = 7 * 24 * 3600;
            ulong WHITELIST_SALE_BEGIN = (ulong)Storage.Get(Storage.CurrentContext, "WHITELIST_SALE_BEGIN").AsBigInteger();
            ulong PRE_SALE_BEGIN = (ulong)Storage.Get(Storage.CurrentContext, "PRE_SALE_BEGIN").AsBigInteger();
            ulong MAIN_SALE_BEGIN = (ulong)Storage.Get(Storage.CurrentContext, "MAIN_SALE_BEGIN").AsBigInteger();
            ulong rate = 0;

            // In the whitelist sale
            if (WHITELIST_SALE_BEGIN < now && now < PRE_SALE_BEGIN)
            {
                // Address that is not in the whitelist cannot get tokens
                if (IsInWhiteList(GetSender()) != 1)
                {
                    return 0;
                }

                rate = (ulong)Storage.Get(Storage.CurrentContext, "WHITELIST_SALE_RATE_NEO").AsBigInteger();
            }
            // Pre-sale
            else if (PRE_SALE_BEGIN <= now && now < PRE_SALE_BEGIN + 4 * WEEK_IN_SECONS)
            {
                if (now < PRE_SALE_BEGIN + WEEK_IN_SECONS)
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "PRE_SALE_WEEK1_RATE_NEO").AsBigInteger();
                }
                else if (now < PRE_SALE_BEGIN + 2 * WEEK_IN_SECONS)
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "PRE_SALE_WEEK2_RATE_NEO").AsBigInteger();
                }
                else if (now < PRE_SALE_BEGIN + 3 * WEEK_IN_SECONS)
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "PRE_SALE_WEEK3_RATE_NEO").AsBigInteger();
                }
                else
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "PRE_SALE_WEEK4_RATE_NEO").AsBigInteger();
                }
            }
            // Main-sale
            else if (MAIN_SALE_BEGIN <= now && now < MAIN_SALE_BEGIN + 4 * WEEK_IN_SECONS)
            {
                if (now < MAIN_SALE_BEGIN + WEEK_IN_SECONS)
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "MAIN_SALE_WEEK1_RATE_NEO").AsBigInteger();
                }
                else if (now < MAIN_SALE_BEGIN + 2 * WEEK_IN_SECONS)
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "MAIN_SALE_WEEK2_RATE_NEO").AsBigInteger();
                }
                else if (now < MAIN_SALE_BEGIN + 3 * WEEK_IN_SECONS)
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "MAIN_SALE_WEEK3_RATE_NEO").AsBigInteger();
                }
                else
                {
                    rate = (ulong)Storage.Get(Storage.CurrentContext, "MAIN_SALE_WEEK4_RATE_NEO").AsBigInteger();
                }
            }

            return rate * factor;
        }

        private static BigInteger GetCurrentHardCap()
        {
            ulong now = Runtime.Time;
            ulong WEEK_IN_SECONS = 7 * 24 * 3600;
            ulong WHITELIST_SALE_BEGIN = (ulong)Storage.Get(Storage.CurrentContext, "WHITELIST_SALE_BEGIN").AsBigInteger();
            ulong PRE_SALE_BEGIN = (ulong)Storage.Get(Storage.CurrentContext, "PRE_SALE_BEGIN").AsBigInteger();
            ulong MAIN_SALE_BEGIN = (ulong)Storage.Get(Storage.CurrentContext, "MAIN_SALE_BEGIN").AsBigInteger();
            BigInteger cap = TOTAL_AMOUNT;

            // In the whitelist sale
            if (WHITELIST_SALE_BEGIN < now && now < PRE_SALE_BEGIN)
            {
                cap = Storage.Get(Storage.CurrentContext, "WHITELIST_HARD_CAP").AsBigInteger() * factor;
            }
            // In pre-sale
            else if (PRE_SALE_BEGIN < now && now < PRE_SALE_BEGIN + 4 * WEEK_IN_SECONS)
            {
                cap = Storage.Get(Storage.CurrentContext, "PRE_SALE_HARD_CAP").AsBigInteger() * factor;
            }
            // In main-sale
            else if (MAIN_SALE_BEGIN < now && now < MAIN_SALE_BEGIN + 4 * WEEK_IN_SECONS)
            {
                cap = Storage.Get(Storage.CurrentContext, "MAIN_SALE_HARD_CAP").AsBigInteger() * factor;
            }

            if (cap < TOTAL_AMOUNT)
            {
                return cap;
            }

            return TOTAL_AMOUNT;
        }

        // Get total valid exchange tokens
        private static ulong GetExchangeableTokens(byte[] sender, ulong value, ulong exchange_rate)
        {
            // Amount that user wants to buy
            ulong desired_tokens = value / neo_decimals * exchange_rate;

            // Current total supply tokens.
            BigInteger total_supply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();

            // Hard cap for current sale phase
            BigInteger hard_cap = GetCurrentHardCap();

            // The available tokens left.
            BigInteger balance_token = hard_cap - total_supply;

            // If no token left, just refund
            if (balance_token <= 0)
            {
                Refund(sender, value);
                return 0;
            }

            // Maximum tokens can be purchased per transfer
            ulong max_tokens_per_transfer = (ulong)Storage.Get(Storage.CurrentContext, "MAX_NEO_PER_TRANSFER").AsBigInteger() * exchange_rate;
            if (max_tokens_per_transfer > 0 && balance_token > max_tokens_per_transfer)
            {
                balance_token = max_tokens_per_transfer;
            }

            // If remaining tokens is not enough, try to buy as much as possible
            if (balance_token < desired_tokens)
            {
                BigInteger exchangeable_tokens = balance_token - (balance_token % exchange_rate);
                Refund(sender, (desired_tokens - exchangeable_tokens) / exchange_rate * neo_decimals);
                return (ulong)exchangeable_tokens;
            }

            return desired_tokens;
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
