<Query Kind="Statements">
  <Namespace>System.Security.Cryptography</Namespace>
</Query>


using (Aes aesAlgorithm = Aes.Create())
{
	aesAlgorithm.GenerateKey();
	aesAlgorithm.GenerateIV();
	
	var initVectorBase64 = Convert.ToBase64String(aesAlgorithm.IV);
	var keyBase64 = Convert.ToBase64String(aesAlgorithm.Key);
	
	Console.WriteLine($"Key : {keyBase64}\nInitialisation Vector : {initVectorBase64}");
	Console.WriteLine($"Aes Cipher Mode : {aesAlgorithm.Mode}");
	Console.WriteLine($"Aes Padding Mode: {aesAlgorithm.Padding}");
	Console.WriteLine($"Aes Key Size : {aesAlgorithm.KeySize}");

	//set the parameters with out keyword


	// Create encryptor object
	ICryptoTransform encryptor = aesAlgorithm.CreateEncryptor();

	byte[] encryptedData;

	//Encryption will be done in a memory stream through a CryptoStream object
	using (MemoryStream ms = new MemoryStream())
	{
		using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
		{
			using (StreamWriter sw = new StreamWriter(cs))
			{
				sw.Write("This is a test text");
			}
			encryptedData = ms.ToArray();
		}
	}
	var outputData = Convert.ToString(encryptedData);
	Console.WriteLine(encryptedData);
}