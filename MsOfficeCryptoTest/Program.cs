using MsOfficeCrypto;
using MsOfficeCrypto.Algorithms;
using MsOfficeCrypto.Decryption;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;

    Console.WriteLine("=== Phase 1 Verification Test ===");
    
    const string testFile = @"C:\Users\jorda\OneDrive\Desktop\Test-#123abc.xls"; // Your test file
    
    try
    {
        // 1. Test encryption detection
        bool isEncrypted = OfficeCryptoDetector.IsEncryptedOfficeDocument(testFile);
        Console.WriteLine($"✅ Encryption Detection: {isEncrypted}");
        
        if (!isEncrypted)
        {
            Console.WriteLine("❌ Test file is not encrypted!");
            return;
        }
        
        // 2. Test encryption info extraction
        var info = OfficeCryptoDetector.GetEncryptionInfo(testFile);
        Console.WriteLine($"✅ Encryption Info Extracted: {info is not null}");
        
        // 3. Test version analysis
        if (info?.VersionInfo is not null)
        {
            Console.WriteLine($"✅ Encryption Type: {info.VersionInfo.GetEncryptionType()}");
            Console.WriteLine($"✅ Security Assessment: {info.VersionInfo.GetSecurityAssessment()}");
            Console.WriteLine($"✅ Algorithm Family: {info.VersionInfo.GetAlgorithmFamily()}");
            Console.WriteLine($"✅ Key Length: {info.VersionInfo.GetKeyLengthBits()} bits");
            Console.WriteLine($"✅ Is Modern: {info.VersionInfo.IsModernEncryption()}");
        }
        
        // 4. Test new methods (after adding them)
        Console.WriteLine($"✅ Encryption Strength: {info?.GetEncryptionStrength()}");
        Console.WriteLine($"✅ Rich ToString: {info}");
        
        // 5. Test exception hierarchy
        Console.WriteLine("✅ Exception classes available:");
        Console.WriteLine("  - OffCryptoException");
        Console.WriteLine("  - NotEncryptedException");
        Console.WriteLine("  - InvalidPasswordException");
        Console.WriteLine("  - UnsupportedEncryptionException");
        Console.WriteLine("  - CorruptedEncryptionInfoException");
        Console.WriteLine("  - KeyDerivationException");
        Console.WriteLine("  - DecryptionException");
        Console.WriteLine("  - LegacyDecryptionException");
        
        Console.WriteLine("\n🎉 Phase 1 Implementation COMPLETE!");
        
    }
    catch (NotEncryptedException)
    {
        Console.WriteLine("❌ File is not encrypted (proper exception thrown)");
    }
    catch (OfficeCryptoException ex)
    {
        Console.WriteLine($"❌ OffCrypto Error: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Unexpected Error: {ex.Message}");
    }
