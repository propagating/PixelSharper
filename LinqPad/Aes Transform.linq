<Query Kind="Program">
  <Namespace>System.Security.Cryptography</Namespace>
</Query>

void Main()
{
	const string text = "Hello world";
	var key = new byte[32];
	var initializationVector = new byte[16];
	
	using (var random = new RNGCryptoServiceProvider())
	{
		random.GetNonZeroBytes(key);
		
	}

	string output;
	string outputEncrypted;

	using (var outputEncryptedStream = new MemoryStream())
	{
		using (var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
		{
			AesCtrTransform(key, initializationVector, inputStream, outputEncryptedStream);
		}

		outputEncryptedStream.Position = 0;
		using (var reader = new StreamReader(outputEncryptedStream, Encoding.UTF8, true, 1024, true))
		{
			outputEncrypted = reader.ReadToEnd();
		}
		outputEncryptedStream.Position = 0;

		using (var outputDecryptedStream = new MemoryStream())
		{
			AesCtrTransform(key, initializationVector, outputEncryptedStream, outputDecryptedStream);

			outputDecryptedStream.Position = 0;
			using (var reader = new StreamReader(outputDecryptedStream))
			{
				output = reader.ReadToEnd();
			}
		}
	}
	
	Console.WriteLine(outputEncrypted);
	Console.WriteLine(text);
	Console.WriteLine(output);
}

// You can define other methods, fields, classes and namespaces here

public static void AesCtrTransform(
	byte[] key, byte[] salt, Stream inputStream, Stream outputStream)
{
	SymmetricAlgorithm aes =
		new AesManaged { Mode = CipherMode.ECB, Padding = PaddingMode.None };

	int blockSize = aes.BlockSize / 8;

	if (salt.Length != blockSize)
	{
		throw new ArgumentException(
			"Salt size must be same as block size " +
			$"(actual: {salt.Length}, expected: {blockSize})");
	}

	byte[] counter = (byte[])salt.Clone();

	Queue<byte> xorMask = new Queue<byte>();

	var zeroIv = new byte[blockSize];
	ICryptoTransform counterEncryptor = aes.CreateEncryptor(key, zeroIv);

	int b;
	while ((b = inputStream.ReadByte()) != -1)
	{
		if (xorMask.Count == 0)
		{
			var counterModeBlock = new byte[blockSize];

			counterEncryptor.TransformBlock(
				counter, 0, counter.Length, counterModeBlock, 0);

			for (var i2 = counter.Length - 1; i2 >= 0; i2--)
			{
				if (++counter[i2] != 0)
				{
					break;
				}
			}

			foreach (var b2 in counterModeBlock)
			{
				xorMask.Enqueue(b2);
			}
		}

		var mask = xorMask.Dequeue();
		outputStream.WriteByte((byte)(((byte)b) ^ mask));
	}
}