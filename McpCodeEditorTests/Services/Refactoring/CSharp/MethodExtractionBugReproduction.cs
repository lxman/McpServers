using McpCodeEditor.Services.Validation;
using McpCodeEditor.Models.Options;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Validation;


namespace McpCodeEditorTests.Services.Refactoring.CSharp;

/// <summary>
/// Test to reproduce and fix C# method extraction bugs with multiple variable modifications
/// </summary>
public class MethodExtractionBugReproduction
{
    public static async Task MainAsync(string[] args)
    {
        Console.WriteLine("=== C# Method Extraction Bug Reproduction ===");
        Console.WriteLine();

        // Test Case 1: Simple extraction (should work)
        await TestSimpleExtractionAsync();
        
        Console.WriteLine();
        Console.WriteLine("=====================================");
        Console.WriteLine();
        
        // Test Case 2: Complex extraction with multiple modified variables (currently failing)
        await TestComplexExtractionAsync();
        
        Console.WriteLine();
        Console.WriteLine("=== Tests Complete ===");
    }

    private static async Task TestSimpleExtractionAsync()
    {
        Console.WriteLine("--- Test 1: Simple Method Extraction ---");
        
        var testCode = @"
public class TestClass
{
    public void TestMethod()
    {
        int x = 5;
        int y = 10;
        int result = x + y;
        Console.WriteLine(result);
    }
}";

        var options = new CSharpExtractionOptions
        {
            NewMethodName = "CalculateSum",
            StartLine = 6,  // int result = x + y;
            EndLine = 6,
            AccessModifier = "private",
            IsStatic = false,
            ReturnType = "int"
        };

        var context = new RefactoringContext
        {
            FilePath = "TestFile.cs",
            FileContent = testCode,
            Language = LanguageType.CSharp
        };

        await TestExtractionAsync("Simple extraction", context, options);
    }

    private static async Task TestComplexExtractionAsync()
    {
        Console.WriteLine("--- Test 2: Complex Method Extraction (Multiple Variables) ---");
        
        const string testCode = @"
public class TestClass
{
    public void ProcessOrders(List<Order> orders)
    {
        int totalPending = 0;
        int pendingCount = 0;
        int totalCompleted = 0;
        int completedCount = 0;
        
        foreach (var order in orders)
        {
            if (order.Status == ""Pending"")
            {
                totalPending += order.Quantity;
                pendingCount++;
            }
            else if (order.Status == ""Completed"")
            {
                totalCompleted += order.Quantity;
                completedCount++;
            }
        }
        
        // These variables are used after the extraction
        Console.WriteLine($""Pending: {totalPending} items in {pendingCount} orders"");
        Console.WriteLine($""Completed: {totalCompleted} items in {completedCount} orders"");
    }
}

public class Order
{
    public string Status { get; set; }
    public int Quantity { get; set; }
}";

        var options = new CSharpExtractionOptions
        {
            NewMethodName = "CalculateOrderStatistics",
            StartLine = 11,  // Start of foreach loop
            EndLine = 21,    // End of foreach loop
            AccessModifier = "private",
            IsStatic = false,
            ReturnType = null // Let analyzer determine
        };

        var context = new RefactoringContext
        {
            FilePath = "TestFile.cs", 
            FileContent = testCode,
            Language = LanguageType.CSharp
        };

        await TestExtractionAsync("Complex extraction (multiple variables)", context, options);
    }

    private static async Task TestExtractionAsync(string testName, RefactoringContext context, CSharpExtractionOptions options)
    {
        Console.WriteLine($"Testing: {testName}");
        Console.WriteLine($"Extracting lines {options.StartLine}-{options.EndLine} into method '{options.NewMethodName}'");
        Console.WriteLine();

        try
        {
            // Create validator instance
            var validator = new ExtractMethodValidator();
            
            // Convert to legacy options for validation
            var legacyOptions = new ExtractMethodOptions
            {
                NewMethodName = options.NewMethodName,
                StartLine = options.StartLine,
                EndLine = options.EndLine,
                IsStatic = options.IsStatic,
                AccessModifier = options.AccessModifier,
                ReturnType = options.ReturnType
            };

            // Perform validation
            Console.WriteLine("Running validation...");
            MethodExtractionValidationResult validationResult = await validator.ValidateExtractionAsync(context.FileContent, legacyOptions);
            
            Console.WriteLine($"Validation Result: {(validationResult.IsValid ? "VALID" : "INVALID")}");
            
            if (validationResult.Errors.Count > 0)
            {
                Console.WriteLine("Errors:");
                foreach (ValidationError error in validationResult.Errors)
                {
                    Console.WriteLine($"  ❌ {error.Description}");
                }
            }
            
            if (validationResult.Warnings.Count > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (ValidationWarning warning in validationResult.Warnings)
                {
                    Console.WriteLine($"  ⚠️  {warning.Description}");
                }
            }

            // Show analysis results
            if (validationResult.Analysis != null)
            {
                CSharpExtractionAnalysis? analysis = validationResult.Analysis;
                Console.WriteLine();
                Console.WriteLine("Analysis Results:");
                Console.WriteLine($"  Suggested Return Type: {analysis.SuggestedReturnType}");
                Console.WriteLine($"  Requires Return Value: {analysis.RequiresReturnValue}");
                Console.WriteLine($"  Return Type Reason: {analysis.ReturnTypeReason}");
                Console.WriteLine($"  Local Variables: {string.Join(", ", analysis.LocalVariables)}");
                Console.WriteLine($"  Modified Variables: {string.Join(", ", analysis.ModifiedVariables)}");
                Console.WriteLine($"  External Variables: {string.Join(", ", analysis.ExternalVariables)}");
                Console.WriteLine($"  Suggested Parameters: {string.Join(", ", analysis.SuggestedParameters)}");
                Console.WriteLine($"  Cyclomatic Complexity Score: {analysis.CyclomaticComplexity}");
            }

            // If validation passes, test the actual extraction logic
            if (validationResult.IsValid)
            {
                Console.WriteLine();
                Console.WriteLine("✅ Validation passed! This extraction should work.");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("❌ Validation failed! This is the bug we need to fix.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception during testing: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}