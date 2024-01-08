using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Globalization;
using SshNet.Security.Cryptography;
using System.Runtime.CompilerServices;

namespace News_Blockchain
{
    class BlockValidator
    {
        private const uint DEFAULT_NBITS = 0x1d00ffff;
        private const int TARGET_BLOCK_TIME = 10;
        private const int BLOCKS_PER_DIFFICULTY_READJUSTMENT = 2016;

        /// <summary>
        /// Function checks given block for any potential rule violations and marks it as
        /// valid in case of no violations.
        /// </summary>
        /// <param name="block"></param>
        /// <returns>true or false</returns>
        public bool ValidateBlock(Block block)
        {
            return true;
        }

        /// <summary>
        /// Given list of transactios merkle root hash is returned.
        /// Every value is hashed twice with SHA256.
        /// In case of odd number of leaves value is hased with itself.
        /// </summary>
        /// <param name="transactions"></param>
        /// <returns>Merkle root hash value</returns>
        private string MerkleRootHash(List<Transaction> transactions)
        {
            List<List<string>> InternalNodes = new List<List<string>>();
            int height = 0;

            InternalNodes.Add(new List<string>());
            //TODO: Add validation that first transaction in the list is coinbase transaction
            foreach (Transaction transaction in transactions)
            {
                InternalNodes[0].Add(Serializator.SerializeToString(transaction));
            }

            while (true)
            {
                InternalNodes.Add(new List<string>());
                height++;

                for (int index = 0; true; index += 2)
                {
                    if (InternalNodes[height - 1].Count == 1)
                        return InternalNodes[height - 1][0];

                    if (index + 1 == InternalNodes[height - 1].Count)
                    {
                        InternalNodes[height].Add(Helpers.ComputeSHA256Hash(
                            InternalNodes[height - 1][index] + InternalNodes[height - 1][index]));
                        break;
                    }
                    if (index == InternalNodes[height - 1].Count)
                        break;

                    InternalNodes[height].Add(Helpers.ComputeSHA256Hash(
                        InternalNodes[height - 1][index] + InternalNodes[height - 1][index + 1]));
                }
            }
        }

        /// <summary>
        /// Function checks if provided hash satisfies nBits requirement.
        /// It is not intended to be used for mining, because target is calculated every time.
        /// </summary>
        /// <param name="headerHash"></param>
        /// <param name="nBits"></param>
        /// <returns>true or false</returns>
        private bool CheckHashDifficultyTarget(string headerHash, uint nBits)
        {
            BigInteger target = DecompressNbits(nBits);

            BigInteger HexHashValue = BigInteger.Parse(headerHash, System.Globalization.NumberStyles.HexNumber);

            if (HexHashValue > target)
                return false;

            return true;
        }

        /// <summary>
        /// Function calculates difficulty, given some nBits value
        /// </summary>
        /// <param name="currentNbits"></param>
        /// <returns>difficulty</returns>
        private uint CalculateDifficulty(uint nBits)
        {
            return (uint)(DecompressNbits(DEFAULT_NBITS) / DecompressNbits(nBits));
        }

        /// <summary>
        /// Function calculates full 256 bit value for nBits from compressed 32 bit int value
        /// </summary>
        /// <param name="nBits"></param>
        /// <returns>decompressed nBits value</returns>
        private BigInteger DecompressNbits(uint nBits)
        {
            int significand = int.Parse(nBits.ToString("X").Substring(2), System.Globalization.NumberStyles.HexNumber);
            int exponent = int.Parse(nBits.ToString("X").Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            return significand * BigInteger.Pow(256, exponent - 3);
        }

        /// <summary>
        /// Given nBits value of previous 14 day interval and time difference in minutes between first and
        /// last block in 14 day interval, function outputs new nBits target
        /// </summary>
        /// <param name="oldNbits"></param>
        /// <param name="timeDifference"></param>
        /// <returns>new nBits target</returns>
        private uint NewDifficulty(uint oldNbits, int timeDifference)
        {
            uint oldDifficulty = CalculateDifficulty(oldNbits);
            double newDifficulty = oldDifficulty * (double)(BLOCKS_PER_DIFFICULTY_READJUSTMENT * TARGET_BLOCK_TIME) / timeDifference;
            uint newNbits = (uint)(DEFAULT_NBITS / newDifficulty);

            if (newNbits > DEFAULT_NBITS)
                return DEFAULT_NBITS;
            return newNbits;
        }

        /// <summary>
        /// Function checks if provided nBits in a block are correct. It also takes into
        /// account difficulty readjustment.
        /// </summary>
        /// <param name="previousBlock"></param>
        /// <param name="newBlock"></param>
        /// <param name="blockHeight"></param>
        /// <returns>true or false</returns>
        private bool EvaluateCorrectnessOfBlockDifficulty(Block previousBlock, Block newBlock, int blockHeight)
        {
            if (blockHeight % 2016 == 0)
            {
                if (newBlock.NBits != NewDifficulty(previousBlock.NBits, 0))
                    return false;
                //TODO: In the above if find elegant way to input time diffrence between fist and last of 2016 blocks
            }

            if (previousBlock.NBits != newBlock.NBits)
                return false;

            return true;
        }
        /// <summary>
        /// Function checks if block size is equal or less than 1MB
        /// </summary>
        /// <param name="block"></param>
        /// <returns>true or false</returns>
        private bool CheckBlockSerializedSize(Block block)
        {
            if (Serializator.SerializeToString(block).Length >= 1024 * 1024)
                return false;

            return true;
        }

        /// <summary>
        /// Function checks if the current block has the same pbhh as the previous block header hash
        /// </summary>
        /// <param name="previousBlock"></param>
        /// <param name="currentBlock"></param>
        /// <returns>true or false</returns>
        private bool CheckForMatchingBlockHeader(Block previousBlock, Block currentBlock)
        {
            if (currentBlock.PreviousBlocKHeaderHash != Helpers.GetBlockHash(previousBlock))
                return false;

            return true;
        }

        /// <summary>
        /// Function calculates base reward for given block height
        /// </summary>
        /// <param name="heigt"></param>
        /// <returns>base reward</returns>
        private double CalculateBaseReward(int heigt)
        {
            return 50 * Math.Pow(0.5, heigt / 210000);
        }

        /// <summary>
        /// Function calculates blocks fees 
        /// </summary>
        /// <param name="block"></param>
        /// <returns>fee sum</returns>
        private double CalculateBlockFees(Block block)
        {
            double fees = 0;
            foreach(Transaction t in block.Transactions)
            {
                double inputValue = 0;
                double outputValue = 0;

                foreach(Transacation_Input ti in t.Inputs)
                {
                    //TODO: change 0 to appropriet function to call value from some output in previous transaction
                    inputValue += 0;
                }

                foreach(Transacation_Output to in t.Outputs)
                {
                    outputValue += to.Value;
                }
                fees += inputValue - outputValue;
            }
            return fees;
        }

        /// <summary>
        /// Function checks if coinbase transaction is correct 
        /// </summary>
        /// <param name="block"></param>
        /// <param name="height"></param>
        /// <returns>true or false</returns>
        private bool CheckCoinbaseTransaction(Block block, int height)
        {
            Transaction coinbaseTransaction = block.Transactions.ElementAt(0);
            double maxCoinbaseValue = CalculateBaseReward(height) + CalculateBlockFees(block);

            if (coinbaseTransaction.Outputs.ElementAt(0).Value > maxCoinbaseValue)
                return false;

            return true;
        }

        /// <summary>
        /// Function evaluates transaction signature
        /// </summary>
        /// <param name="transacation"></param>
        /// <param name="pubkey"></param>
        /// <param name="senderPublickKey"></param>
        /// <returns>true or false</returns>
        private bool CheckTransactionSignature(Transacation_Input transaction, Transacation_Output transaction_Output, string pubkey, string senderPublickKey)
        {
            string pubkeyCoppy = pubkey;

            string pubkeyHash = Helpers.ComputeSHA256Hash(pubkeyCoppy, 2);
            string publickKeyHash = Helpers.ComputeSHA256Hash(senderPublickKey, 2);

            byte[] data = Encoding.ASCII.GetBytes("message");
            byte[] hash = System.Security.Cryptography.SHA256.Create().ComputeHash(data);

            RSAParameters sharedParameters;
            byte[] signedHash;
            //generate signature
            using (RSA rsa = RSA.Create())
            {
                sharedParameters = rsa.ExportParameters(false);

                RSAPKCS1SignatureFormatter rsaFormatter = new(rsa);
                rsaFormatter.SetHashAlgorithm(nameof(Helpers.ComputeSHA256Hash));

                signedHash = rsaFormatter.CreateSignature(hash);
            }

            //verify signature 
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(sharedParameters);

                RSAPKCS1SignatureDeformatter rsaDeformatter = new(rsa);
                rsaDeformatter.SetHashAlgorithm(nameof(Helpers.ComputeSHA256Hash));

                if (!rsaDeformatter.VerifySignature(hash, signedHash))
                    return false;
            }

            if (pubkeyHash != publickKeyHash)
             return false;

            return true;
        }

        /// <summary>
        /// Function checks if block height of a coinbase transaction is greater than 100
        /// </summary>
        /// /// <param name="block">block that contains coinbase trx</param>
        /// /// <param name="trxHeight">blocks height</param>
        /// <param name="currentHeight">current height</param>
        /// <returns>ture of false</returns>
        private bool CoinbaseTransactionMaturity(Block block, int trxHeight, int currentHeight)
        {
            if (!CheckCoinbaseTransaction(block, trxHeight))
                return false;

            if (currentHeight - trxHeight < 100)
                return false;

            return true;
        }

    }
}