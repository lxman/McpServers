using McpCodeEditor.Models.Options;
using McpCodeEditor.Models.Validation;
using McpCodeEditor.Services.Validation;

namespace McpCodeEditorTests.Services.Refactoring.CSharp
{
    /// <summary>
    /// Unit tests for the ExtractMethodValidator class.
    /// Focuses on variable analysis and detection logic.
    /// </summary>
    public class ExtractMethodValidatorTests
    {
        private readonly ExtractMethodValidator _validator;

        public ExtractMethodValidatorTests()
        {
            _validator = new ExtractMethodValidator();
        }

        #region Variable Detection Tests

        [Fact]
        public async Task ValidateExtraction_DetectsVariableDeclarations()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    public class Calculator
    {
        public void Calculate()
        {
            int x = 10;
            int y = 20;
            
            // Extract these lines
            int result = x + y;
            result = result * 2;
            Console.WriteLine($""Result: {result}"");
            
            // Use result after
            int finalValue = result + 100;
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "CalculateDoubledSum",
                StartLine = 14,
                EndLine = 16
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            
            // Check that 'result' is detected as a local variable (declared in selection)
            Assert.Contains("result", result.Analysis.LocalVariables);
            
            // Check that x and y are detected as external variables
            Assert.Contains("x", result.Analysis.ExternalVariables);
            Assert.Contains("y", result.Analysis.ExternalVariables);
            
            // Should suggest parameters for x and y
            Assert.Contains(result.Analysis.SuggestedParameters, p => p.Contains("x"));
            Assert.Contains(result.Analysis.SuggestedParameters, p => p.Contains("y"));
            
            // Should require return value since result is used after
            Assert.True(result.Analysis.RequiresReturnValue);
            Assert.Equal("int", result.Analysis.SuggestedReturnType);
        }

        [Fact]
        public async Task ValidateExtraction_DetectsMultipleModifiedVariables()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    public class Processor
    {
        public void Process()
        {
            var items = new int[] { 1, 2, 3, 4, 5 };
            
            // Extract this block
            int sum = 0;
            int count = 0;
            int max = items[0];
            
            foreach (var item in items)
            {
                sum += item;
                count++;
                if (item > max) max = item;
            }
            
            double average = sum / (double)count;
            // End extraction
            
            Console.WriteLine($""Sum: {sum}, Count: {count}, Max: {max}, Avg: {average}"");
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "CalculateStatistics",
                StartLine = 13,
                EndLine = 24
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            
            // All variables should be detected as local (declared in selection)
            Assert.Contains("sum", result.Analysis.LocalVariables);
            Assert.Contains("count", result.Analysis.LocalVariables);
            Assert.Contains("max", result.Analysis.LocalVariables);
            Assert.Contains("average", result.Analysis.LocalVariables);
            
            // Should have warnings about multiple modified variables
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Description.Contains("variable") || w.Description.Contains("modified"));
        }

        [Fact]
        public async Task ValidateExtraction_DistinguishesDeclarationFromAssignment()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    public class Calculator
    {
        public void Calculate()
        {
            int result;  // Declaration without initialization
            int x = 10;
            int y = 20;
            
            // Extract from here
            result = x + y;  // Assignment, not declaration
            result = result * 2;
            // Extract to here
            
            Console.WriteLine($""Result: {result}"");
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "CalculateValue",
                StartLine = 14,
                EndLine = 16
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            
            // 'result' should be external (declared before selection)
            Assert.Contains("result", result.Analysis.ExternalVariables);
            Assert.DoesNotContain("result", result.Analysis.LocalVariables);
            
            // Should be modified
            Assert.Contains("result", result.Analysis.ModifiedVariables);
        }

        #endregion

        #region Return Type Analysis Tests

        [Fact]
        public async Task ValidateExtraction_DetectsReturnStatements()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    public class Calculator
    {
        public int Calculate(int value)
        {
            // Extract this
            if (value > 100)
                return value * 2;
            return value;
            // End extraction
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "ProcessValue",
                StartLine = 10,
                EndLine = 13
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            Assert.True(result.Analysis.HasReturnStatements);
            
            // Should have warning about return statements
            Assert.Contains(result.Warnings, w => w.Description.Contains("return"));
        }

        [Fact]
        public async Task ValidateExtraction_SuggestsCorrectReturnType()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    public class Calculator
    {
        public void Process()
        {
            int x = 10;
            int y = 20;
            
            // Extract this
            double result = (double)x / y;
            // End extraction
            
            Console.WriteLine($""Result: {result}"");
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "Divide",
                StartLine = 13,
                EndLine = 14
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            Assert.True(result.Analysis.RequiresReturnValue);
            // The semantic analyzer should detect double as return type
            Assert.Contains("double", result.Analysis.SuggestedReturnType?.ToLower());
        }

        #endregion

        #region Complexity Analysis Tests

        [Fact]
        public async Task ValidateExtraction_CalculatesCyclomaticComplexity()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    public class Processor
    {
        public void Process(int value)
        {
            // Extract this complex block
            if (value > 0)
            {
                if (value > 100)
                {
                    Console.WriteLine(""Large"");
                }
                else if (value > 50)
                {
                    Console.WriteLine(""Medium"");
                }
                else
                {
                    Console.WriteLine(""Small"");
                }
            }
            else
            {
                Console.WriteLine(""Negative or zero"");
            }
            // End extraction
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "ClassifyValue",
                StartLine = 10,
                EndLine = 29
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Analysis);
            Assert.True(result.Analysis.CyclomaticComplexity > 1);
            Assert.True(result.Analysis.HasComplexControlFlow);
        }

        #endregion

        #region Validation Error Tests

        [Fact]
        public async Task ValidateExtraction_InvalidMethodName_ReturnsError()
        {
            // Arrange
            var sourceCode = @"
public class Test
{
    public void Method()
    {
        int x = 10;
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "123Invalid", // Invalid name starting with number
                StartLine = 6,
                EndLine = 6
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Description.Contains("Method name"));
        }

        [Fact]
        public async Task ValidateExtraction_ReservedKeyword_ReturnsError()
        {
            // Arrange
            var sourceCode = @"
public class Test
{
    public void Method()
    {
        int x = 10;
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "return", // Reserved keyword
                StartLine = 6,
                EndLine = 6
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Description.Contains("reserved"));
        }

        [Fact]
        public async Task ValidateExtraction_OutsideMethodScope_ReturnsError()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace Test
{
    // Try to extract at class level
    private int field = 10;
    
    public class Calculator
    {
        public void Method()
        {
            Console.WriteLine(field);
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "ExtractedMethod",
                StartLine = 7,
                EndLine = 7
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Description.Contains("method scope"));
        }

        [Fact]
        public async Task ValidateExtraction_DuplicateMethodName_ReturnsError()
        {
            // Arrange
            var sourceCode = @"
public class Test
{
    public void ExistingMethod()
    {
        Console.WriteLine(""Existing"");
    }
    
    public void Process()
    {
        // Try to extract with duplicate name
        int x = 10;
        Console.WriteLine(x);
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "ExistingMethod", // Duplicate name
                StartLine = 12,
                EndLine = 13
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Description.Contains("already exists"));
        }

        #endregion

        #region Warning Tests

        [Fact]
        public async Task ValidateExtraction_NonPascalCase_GeneratesWarning()
        {
            // Arrange
            var sourceCode = @"
public class Test
{
    public void Method()
    {
        int x = 10;
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "calculateValue", // camelCase instead of PascalCase
                StartLine = 6,
                EndLine = 6
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid); // Should still be valid
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Description.Contains("PascalCase"));
        }

        [Fact]
        public async Task ValidateExtraction_HighComplexity_GeneratesWarning()
        {
            // Arrange
            var sourceCode = @"
public class Test
{
    public void ComplexMethod()
    {
        // Very complex code with high cyclomatic complexity
        for (int i = 0; i < 10; i++)
        {
            if (i == 0) Console.WriteLine(""0"");
            else if (i == 1) Console.WriteLine(""1"");
            else if (i == 2) Console.WriteLine(""2"");
            else if (i == 3) Console.WriteLine(""3"");
            else if (i == 4) Console.WriteLine(""4"");
            else if (i == 5) Console.WriteLine(""5"");
            else if (i == 6) Console.WriteLine(""6"");
            else if (i == 7) Console.WriteLine(""7"");
            else if (i == 8) Console.WriteLine(""8"");
            else if (i == 9) Console.WriteLine(""9"");
        }
    }
}";

            var options = new ExtractMethodOptions
            {
                NewMethodName = "ProcessComplexLogic",
                StartLine = 7,
                EndLine = 19
            };

            // Act
            MethodExtractionValidationResult result = await _validator.ValidateExtractionAsync(sourceCode, options);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Description.Contains("complexity") || w.Description.Contains("CC:"));
        }

        #endregion
    }
}
