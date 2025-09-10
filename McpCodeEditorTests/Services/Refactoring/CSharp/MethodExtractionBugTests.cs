using McpCodeEditor.Models.Options;
using McpCodeEditor.Models.Validation;
using McpCodeEditor.Services.Validation;

namespace McpCodeEditorTests.Services.Refactoring.CSharp
{
    /// <summary>
    /// Focused tests for specific bugs found in C# method extraction
    /// </summary>
    public class MethodExtractionBugTests
    {
        private readonly ExtractMethodValidator _validator = new();

        [Fact]
        public async Task Bug_Should_Detect_Variable_Declared_In_Extraction_And_Used_After()
        {
            // This is the bug from SimpleExtractionTest.cs
            // When extracting "int result = x + y;" and result is used after,
            // the system should know result needs to be declared with type in the call
            
            var sourceCode = @"
using System;

namespace SimpleTest
{
    public class Calculator
    {
        public void Calculate()
        {
            int x = 10;
            int y = 20;
            
            // Simple calculation to extract
            int result = x + y;
            result = result * 2;
            Console.WriteLine($""Result: {result}"");
            
            // Use result after extraction
            int finalValue = result + 100;
            Console.WriteLine($""Final: {finalValue}"");
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "CalculateDoubledSum",
                StartLine = 13,
                EndLine = 15
            };

            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // The validator should detect:
            // 1. 'result' is declared in the selected code
            // 2. 'result' is used after the extraction
            // 3. Therefore, it needs to be returned
            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            
            // Check that 'result' is identified as a local variable
            Assert.Contains("result", result.Analysis.LocalVariables);
            
            // Check that it requires a return value
            Assert.True(result.Analysis.RequiresReturnValue);
            Assert.Equal("int", result.Analysis.SuggestedReturnType);
        }

        [Fact]
        public async Task Bug_Should_Detect_Multiple_Modified_Variables()
        {
            // This is the bug from TestMethodExtraction.cs
            // The system wasn't detecting that sum, max, min, average are all modified
            
            var sourceCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestExtraction
{
    public class DataProcessor
    {
        private List<int> data = new List<int>();
        
        public void ProcessData()
        {
            // Initialize data
            for (int i = 0; i < 100; i++)
            {
                data.Add(i * 2);
            }
            
            // Calculate statistics - THIS SHOULD BE EXTRACTED
            int sum = 0;
            int max = int.MinValue;
            int min = int.MaxValue;
            
            foreach (var value in data)
            {
                sum += value;
                if (value > max) max = value;
                if (value < min) min = value;
            }
            
            double average = sum / (double)data.Count;
            
            // Display results
            Console.WriteLine($""Sum: {sum}"");
            Console.WriteLine($""Average: {average}"");
            Console.WriteLine($""Max: {max}"");
            Console.WriteLine($""Min: {min}"");
            
            // Process outliers
            var outliers = data.Where(x => x > average * 2).ToList();
            Console.WriteLine($""Found {outliers.Count} outliers"");
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "CalculateStatistics",
                StartLine = 20,
                EndLine = 32
            };

            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            
            // All four variables should be in LocalVariables (declared in extraction)
            Assert.Contains("sum", result.Analysis.LocalVariables);
            Assert.Contains("max", result.Analysis.LocalVariables);
            Assert.Contains("min", result.Analysis.LocalVariables);
            Assert.Contains("average", result.Analysis.LocalVariables);
            
            // Should have warnings about multiple variables
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Description.Contains("variable"));
        }

        [Fact]
        public async Task Should_Distinguish_External_From_Local_Variables()
        {
            var sourceCode = @"
public class Test
{
    public void Method()
    {
        int external = 10;
        
        // Extract this
        int local = external * 2;
        Console.WriteLine(local);
        
        // Use both after
        Console.WriteLine($""{external}, {local}"");
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "Process",
                StartLine = 9,
                EndLine = 10
            };

            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            
            // 'external' should be in ExternalVariables (declared before extraction)
            Assert.Contains("external", result.Analysis.ExternalVariables);
            
            // 'local' should be in LocalVariables (declared in extraction)
            Assert.Contains("local", result.Analysis.LocalVariables);
            
            // Should suggest 'external' as parameter
            Assert.Contains(result.Analysis.SuggestedParameters, p => p.Contains("external"));
        }
    }
}
