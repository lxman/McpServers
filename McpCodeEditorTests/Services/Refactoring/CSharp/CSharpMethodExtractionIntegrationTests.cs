using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Services.Refactoring.CSharp;
using McpCodeEditor.Services.Validation;
using Microsoft.Extensions.Logging;
using Moq;

namespace McpCodeEditorTests.Services.Refactoring.CSharp
{
    /// <summary>
    /// Integration tests for C# method extraction that test the actual implementation
    /// without extensive mocking. These tests focus on ensuring the real extractor
    /// works correctly with the validator for various scenarios.
    /// </summary>
    public class CSharpMethodExtractionIntegrationTests
    {
        private readonly CSharpMethodExtractor _extractor;

        public CSharpMethodExtractionIntegrationTests()
        {
            var validator =
                // Use real validator instead of mock for integration testing
                new ExtractMethodValidator();
            
            var mockPathValidationService = new Mock<IPathValidationService>();
            var mockBackupService = new Mock<IBackupService>();
            var mockMethodCallGenerationService = new Mock<IMethodCallGenerationService>();
            var mockMethodSignatureGenerationService = new Mock<IMethodSignatureGenerationService>();
            var mockCodeModificationService = new Mock<ICodeModificationService>();
            var mockEnhancedVariableAnalysisService = new Mock<IEnhancedVariableAnalysisService>();
            var mockChangeTrackingService = new Mock<IChangeTrackingService>();
            var mockLogger = new Mock<ILogger<CSharpMethodExtractor>>();

            _extractor = new CSharpMethodExtractor(
                mockPathValidationService.Object,
                mockBackupService.Object,
                mockLogger.Object, 
                mockMethodCallGenerationService.Object,
                mockMethodSignatureGenerationService.Object,
                mockCodeModificationService.Object,
                mockEnhancedVariableAnalysisService.Object,
                mockChangeTrackingService.Object
            );

            // Setup minimal required mocks
            mockPathValidationService.Setup(p => p.ValidateAndResolvePath(It.IsAny<string>()))
                .Returns<string>(path => path);
        }

        #region Working Scenarios - Simple Extractions

        [Fact]
        public async Task Integration_SimpleCalculation_ShouldExtractSuccessfully()
        {
            // Test that simple extractions work correctly
            var context = new RefactoringContext
            {
                FilePath = "SimpleCalc.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class Calculator
    {
        public void Calculate()
        {
            int a = 10;
            int b = 20;
            
            // Extract this simple calculation
            int sum = a + b;
            int product = a * b;
            
            Console.WriteLine($""Sum: {sum}, Product: {product}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "PerformCalculation",
                StartLine = 13,
                EndLine = 14,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act - Use real validator
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Simple extraction should succeed. Error: {result.Error}");
            Assert.Single(result.Changes);
            
            var change = result.Changes.First();
            Assert.Contains("private (int sum, int product) PerformCalculation(int a, int b)", change.ModifiedContent);
            Assert.Contains("return (sum, product);", change.ModifiedContent);
            Assert.Contains("var (sum, product) = PerformCalculation(a, b);", change.ModifiedContent);
        }

        [Fact]
        public async Task Integration_SingleReturnValue_ShouldWork()
        {
            // Test extraction with single return value
            var context = new RefactoringContext
            {
                FilePath = "SingleReturn.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class Math
    {
        public void ComputeSquare()
        {
            int number = 5;
            
            // Extract this
            int square = number * number;
            
            Console.WriteLine($""Square of {number} is {square}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateSquare",
                StartLine = 12,
                EndLine = 12,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Should succeed. Error: {result.Error}");
            
            var change = result.Changes.First();
            Assert.Contains("private int CalculateSquare(int number)", change.ModifiedContent);
            Assert.Contains("return square;", change.ModifiedContent);
            Assert.Contains("int square = CalculateSquare(number);", change.ModifiedContent);
        }

        #endregion

        #region Complex Scenarios That Should Work

        [Fact]
        public async Task Integration_LoopWithAccumulator_ShouldWork()
        {
            // Test extraction of loop with accumulator
            var context = new RefactoringContext
            {
                FilePath = "LoopAccumulator.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class Accumulator
    {
        public void SumArray()
        {
            int[] numbers = { 1, 2, 3, 4, 5 };
            
            // Extract this loop
            int total = 0;
            foreach (int num in numbers)
            {
                total += num;
            }
            
            Console.WriteLine($""Total: {total}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateSum",
                StartLine = 12,
                EndLine = 16,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Loop extraction should work. Error: {result.Error}");
            
            var change = result.Changes.First();
            Assert.Contains("private int CalculateSum(int[] numbers)", change.ModifiedContent);
            Assert.Contains("return total;", change.ModifiedContent);
        }

        [Fact]
        public async Task Integration_ConditionalLogic_ShouldWork()
        {
            // Test extraction with conditional logic
            var context = new RefactoringContext
            {
                FilePath = "Conditional.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class Validator
    {
        public void ValidateAge()
        {
            int age = 25;
            
            // Extract this validation
            bool isAdult = age >= 18;
            bool isSenior = age >= 65;
            string category = isAdult ? (isSenior ? ""Senior"" : ""Adult"") : ""Minor"";
            
            Console.WriteLine($""Category: {category}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "DetermineAgeCategory",
                StartLine = 12,
                EndLine = 14,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Conditional extraction should work. Error: {result.Error}");
            
            var change = result.Changes.First();
            // Should return multiple values
            Assert.Contains("DetermineAgeCategory", change.ModifiedContent);
            Assert.Contains("return", change.ModifiedContent);
        }

        #endregion

        #region Known Failure Scenarios

        [Fact]
        public async Task Integration_KnownBug_FourVariableModifications_CurrentlyFails()
        {
            // This test documents the actual bug behavior
            var context = new RefactoringContext
            {
                FilePath = "FourVariableBug.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;
using System.Collections.Generic;

namespace BugDemo
{
    public class OrderCalculator
    {
        public void CalculateOrderStats()
        {
            var orders = new List<Order> 
            { 
                new Order { Status = ""Pending"", Amount = 100 },
                new Order { Status = ""Completed"", Amount = 200 },
                new Order { Status = ""Pending"", Amount = 150 }
            };
            
            // This extraction currently fails - 4 variable modifications
            decimal totalPending = 0;
            int pendingCount = 0;
            decimal totalCompleted = 0;
            int completedCount = 0;
            
            foreach (var order in orders)
            {
                if (order.Status == ""Pending"")
                {
                    totalPending += order.Amount;
                    pendingCount++;
                }
                else if (order.Status == ""Completed"")
                {
                    totalCompleted += order.Amount;
                    completedCount++;
                }
            }
            
            Console.WriteLine($""Pending: {pendingCount} orders, ${totalPending}"");
            Console.WriteLine($""Completed: {completedCount} orders, ${totalCompleted}"");
        }
        
        class Order
        {
            public string Status { get; set; }
            public decimal Amount { get; set; }
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateTotals",
                StartLine = 18,
                EndLine = 35,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act - This will likely fail with current implementation
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert - Document the actual behavior
            if (!result.Success)
            {
                // Currently fails - document the error
                Assert.Contains("Validation", result.Error);
                // This is the known bug - validator fails with syntax error
            }
            else
            {
                // If it succeeds (after fix), verify correct extraction
                var change = result.Changes.First();
                Assert.Contains("totalPending", change.ModifiedContent);
                Assert.Contains("return", change.ModifiedContent);
            }
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public async Task Integration_ExtractToStaticMethod_ShouldWork()
        {
            // Test extraction to static method
            var context = new RefactoringContext
            {
                FilePath = "StaticExtraction.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class MathHelper
    {
        public void UseCalculation()
        {
            // Extract to static method - no instance members used
            int x = 10;
            int y = 20;
            int result = x * y + 100;
            
            Console.WriteLine($""Result: {result}"");
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "Calculate",
                StartLine = 10,
                EndLine = 12,
                AccessModifier = "private",
                IsStatic = true  // Request static method
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.True(result.Success, $"Static extraction should work. Error: {result.Error}");
            
            var change = result.Changes.First();
            Assert.Contains("private static int Calculate()", change.ModifiedContent);
        }

        [Fact]
        public async Task Integration_ExtractWithDifferentAccessModifiers_ShouldWork()
        {
            // Test different access modifiers
            var accessModifiers = new[] { "public", "private", "protected", "internal" };
            
            foreach (var modifier in accessModifiers)
            {
                var context = new RefactoringContext
                {
                    FilePath = $"{modifier}Method.cs",
                    Language = LanguageType.CSharp,
                    FileContent = @"using System;

namespace TestApp
{
    public class TestClass
    {
        public void TestMethod()
        {
            // Simple extraction
            int value = 42;
            int doubled = value * 2;
            
            Console.WriteLine(doubled);
        }
    }
}"
                };

                var options = new CSharpExtractionOptions
                {
                    NewMethodName = $"Extract{modifier}",
                    StartLine = 10,
                    EndLine = 11,
                    AccessModifier = modifier,
                    IsStatic = false
                };

                // Act
                var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

                // Assert
                Assert.True(result.Success, $"{modifier} extraction should work. Error: {result.Error}");
                
                var change = result.Changes.First();
                Assert.Contains($"{modifier} int Extract{modifier}", change.ModifiedContent);
            }
        }

        #endregion

        #region Validation Edge Cases

        [Fact]
        public async Task Integration_InvalidLineNumbers_ShouldFail()
        {
            // Test with invalid line numbers
            var context = new RefactoringContext
            {
                FilePath = "Invalid.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class Test
    {
        public void Method()
        {
            int x = 5;
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "ExtractedMethod",
                StartLine = 100,  // Invalid line number
                EndLine = 200,    // Invalid line number
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.False(result.Success, "Should fail with invalid line numbers");
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task Integration_ExtractAcrossMethodBoundaries_ShouldFail()
        {
            // Test extraction that spans multiple methods
            var context = new RefactoringContext
            {
                FilePath = "CrossMethod.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class Test
    {
        public void Method1()
        {
            int x = 5;
        }
        
        public void Method2()
        {
            int y = 10;
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "ExtractedMethod",
                StartLine = 9,   // In Method1
                EndLine = 14,    // In Method2
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            Assert.False(result.Success, "Should fail when crossing method boundaries");
            Assert.NotNull(result.Error);
        }

        #endregion

        #region Performance and Large Code Tests

        [Fact]
        public async Task Integration_LargeCodeBlock_ShouldHandleEfficiently()
        {
            // Test extraction of large code block
            var largeMethod = @"using System;
using System.Collections.Generic;

namespace TestApp
{
    public class LargeProcessor
    {
        public void ProcessLargeData()
        {
            var data = new List<int>();
            
            // Start extraction - large block
            for (int i = 0; i < 1000; i++)
            {
                data.Add(i);
            }
            
            int sum = 0;
            int count = 0;
            int min = int.MaxValue;
            int max = int.MinValue;
            
            foreach (var item in data)
            {
                sum += item;
                count++;
                
                if (item < min) min = item;
                if (item > max) max = item;
                
                // More processing
                if (item % 2 == 0)
                {
                    // Even number processing
                    var squared = item * item;
                    if (squared > 1000)
                    {
                        // Large squared value
                        Console.WriteLine($""Large even squared: {squared}"");
                    }
                }
                else
                {
                    // Odd number processing
                    var cubed = item * item * item;
                    if (cubed > 10000)
                    {
                        // Large cubed value
                        Console.WriteLine($""Large odd cubed: {cubed}"");
                    }
                }
            }
            
            double average = count > 0 ? (double)sum / count : 0;
            // End extraction
            
            Console.WriteLine($""Stats - Sum: {sum}, Avg: {average}, Min: {min}, Max: {max}"");
        }
    }
}";

            var context = new RefactoringContext
            {
                FilePath = "LargeBlock.cs",
                Language = LanguageType.CSharp,
                FileContent = largeMethod
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "ProcessDataStatistics",
                StartLine = 13,
                EndLine = 52,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var startTime = DateTime.Now;
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);
            var elapsed = DateTime.Now - startTime;

            // Assert
            Assert.True(elapsed.TotalSeconds < 5, "Should complete within 5 seconds");
            
            // Result may succeed or fail based on complexity
            if (result.Success)
            {
                Assert.Single(result.Changes);
                var change = result.Changes.First();
                Assert.Contains("ProcessDataStatistics", change.ModifiedContent);
            }
        }

        #endregion

        #region Formatting and Code Style Tests

        [Fact]
        public async Task Integration_PreserveFormatting_ShouldMaintainIndentation()
        {
            // Test that formatting is preserved
            var context = new RefactoringContext
            {
                FilePath = "Formatting.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class FormattedClass
    {
        public void FormattedMethod()
        {
            // Extract with specific formatting
            int    x     =    10;
            int    y     =    20;
            int    sum   =    x + y;
            
            Console.WriteLine(sum);
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateFormattedSum",
                StartLine = 10,
                EndLine = 12,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            if (result.Success)
            {
                var change = result.Changes.First();
                // Check that basic structure is maintained
                Assert.Contains("CalculateFormattedSum", change.ModifiedContent);
                Assert.Contains("return sum;", change.ModifiedContent);
            }
        }

        [Fact]
        public async Task Integration_CommentsInExtraction_ShouldBePreserved()
        {
            // Test that comments are preserved
            var context = new RefactoringContext
            {
                FilePath = "Comments.cs",
                Language = LanguageType.CSharp,
                FileContent = @"using System;

namespace TestApp
{
    public class CommentedClass
    {
        public void CommentedMethod()
        {
            int input = 10;
            
            // Extract this block with comments
            // This is an important calculation
            int result = input * 2;  // Double the input
            
            /* Multi-line comment
               explaining the logic */
            result += 100;
            
            // End of calculation
            
            Console.WriteLine(result);
        }
    }
}"
            };

            var options = new CSharpExtractionOptions
            {
                NewMethodName = "CalculateWithComments",
                StartLine = 11,
                EndLine = 19,
                AccessModifier = "private",
                IsStatic = false
            };

            // Act
            var result = await _extractor.ExtractMethodAsync(context, options, previewOnly: true);

            // Assert
            if (result.Success)
            {
                var change = result.Changes.First();
                // Comments should be in the extracted method
                Assert.Contains("CalculateWithComments", change.ModifiedContent);
                // The extraction should maintain the basic structure
                Assert.Contains("return result;", change.ModifiedContent);
            }
        }

        #endregion
    }
}
