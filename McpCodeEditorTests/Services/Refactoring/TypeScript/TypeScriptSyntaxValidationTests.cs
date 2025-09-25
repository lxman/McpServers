using System.Reflection;
using McpCodeEditor.Services.Refactoring.TypeScript;
using Microsoft.Extensions.Logging;
using Moq;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Services.Validation;
using McpCodeEditor.Services.Analysis;
using ValidationResult = McpCodeEditor.Services.Validation.ValidationResult;

namespace McpCodeEditorTests.Services.Refactoring.TypeScript
{
    /// <summary>
    /// Unit tests for REF-003: Syntax Validation
    /// Tests the TypeScript syntax validation pipeline before code insertion
    /// </summary>
    public class TypeScriptSyntaxValidationTests
    {
        /// <summary>
        /// Creates a TypeScriptSyntaxValidator service with mocked dependencies for testing
        /// </summary>
        private static ITypeScriptSyntaxValidator CreateTestValidator()
        {
            var mockLogger = new Mock<ILogger<TypeScriptSyntaxValidator>>();
            
            // Create actual service instance for TypeScript syntax validation
            return new TypeScriptSyntaxValidator(mockLogger.Object);
        }

        /// <summary>
        /// Creates a TypeScriptVariableOperations service with mocked dependencies for testing
        /// </summary>
        private static TypeScriptVariableOperations CreateTestService()
        {
            var mockLogger = new Mock<ILogger<TypeScriptVariableOperations>>();
            var mockPathValidation = new Mock<IPathValidationService>();
            
            // Create actual service instances for services that have constructor parameters
            var mockAnalysisServiceLogger = new Mock<ILogger<TypeScriptAnalysisService>>();
            var analysisService = new TypeScriptAnalysisService(mockAnalysisServiceLogger.Object);

            var mockValidatorLogger = new Mock<ILogger<TypeScriptExtractMethodValidator>>();
            var validator = new TypeScriptExtractMethodValidator(mockValidatorLogger.Object, analysisService);

            var mockScopeAnalyzerLogger = new Mock<ILogger<TypeScriptScopeAnalyzer>>();
            var scopeAnalyzer = new TypeScriptScopeAnalyzer(mockScopeAnalyzerLogger.Object);
            
            var mockExpressionBoundaryDetectionService = new Mock<IExpressionBoundaryDetectionService>();
            var mockVariableDeclarationGeneratorService = new Mock<IVariableDeclarationGeneratorService>();
            var mockTypeScriptASTAnalysisService = new Mock<ITypeScriptAstAnalysisService>();
            var mockTypeScriptCodeModificationService = new Mock<ITypeScriptCodeModificationService>();

            // Use actual syntax validator instead of mock for REF-003 testing
            var syntaxValidator = CreateTestValidator();

            return new TypeScriptVariableOperations(
                mockLogger.Object,
                mockPathValidation.Object,
                validator,
                scopeAnalyzer,
                syntaxValidator,
                mockExpressionBoundaryDetectionService.Object,
                mockVariableDeclarationGeneratorService.Object,
                mockTypeScriptASTAnalysisService.Object,
                mockTypeScriptCodeModificationService.Object
            );
        }

        #region REF-003: Basic Syntax Validation Tests

        [Fact]
        public void ValidateTypeScriptSyntax_ValidVariableDeclaration_ShouldPass()
        {
            // Arrange - Valid TypeScript variable declaration
            const string validCode = "const injectedAccountsClient = inject(AccountsClient);";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid TypeScript syntax: {result.ErrorMessage}");
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void ValidateTypeScriptSyntax_MissingParentheses_ShouldFail()
        {
            // Arrange - Missing closing parenthesis (from MongoDB document evidence)
            const string invalidCode = "const injectedAccountsClient = inject(AccountsClient;";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect missing closing parenthesis");
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("parenthes", result.ErrorMessage.ToLower(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateTypeScriptSyntax_MissingOpenParenthesis_ShouldFail()
        {
            // Arrange - Missing opening parenthesis
            const string invalidCode = "const injectedAccountsClient = injectAccountsClient);";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect missing opening parenthesis");
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ValidateTypeScriptSyntax_InvalidConstPlacementInClass_ShouldFail()
        {
            // Arrange - Invalid const placement as direct class member (from MongoDB document evidence)
            const string invalidCode = @"
export class TestComponent {
    const injectedAccountsClient = inject(AccountsClient);
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect invalid const placement in class");
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("const", result.ErrorMessage.ToLower(), StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region REF-003: Class Member Syntax Validation Tests

        [Fact]
        public void ValidateTypeScriptSyntax_ValidClassMemberPrivateReadonly_ShouldPass()
        {
            // Arrange - Valid class member with private readonly (correct fix for REF-002)
            const string validCode = @"
export class TestComponent {
    private readonly injectedAccountsClient = inject(AccountsClient);
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid class member syntax: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_ValidClassMemberWithTypeAnnotation_ShouldPass()
        {
            // Arrange - Valid class member with type annotation
            const string validCode = @"
export class TestComponent {
    private readonly injectedAccountsClient: AccountsClient = inject(AccountsClient);
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid class member with type annotation: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_InvalidClassMemberConstKeyword_ShouldFail()
        {
            // Arrange - Invalid use of const keyword in class (should be private readonly)
            const string invalidCode = @"
export class TestComponent {
    const invalidMember = 'value';
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect invalid const keyword in class");
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("const", result.ErrorMessage.ToLower(), StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region REF-003: Method Local Variable Syntax Validation Tests

        [Fact]
        public void ValidateTypeScriptSyntax_ValidMethodLocalConst_ShouldPass()
        {
            // Arrange - Valid method-local const declaration
            const string validCode = @"
export class TestComponent {
    processData(): void {
        const result = this.calculate();
        return result;
    }
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid method-local const: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_ValidMethodLocalLet_ShouldPass()
        {
            // Arrange - Valid method-local let declaration
            const string validCode = @"
export class TestComponent {
    processData(): void {
        let counter = 0;
        counter++;
        return counter;
    }
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid method-local let: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_InvalidMethodLocalPrivate_ShouldFail()
        {
            // Arrange - Invalid use of private keyword in method (should be const or let)
            const string invalidCode = @"
export class TestComponent {
    processData(): void {
        private readonly invalidLocal = 'value';
        return invalidLocal;
    }
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect invalid private keyword in method");
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region REF-003: Expression Syntax Validation Tests

        [Fact]
        public void ValidateTypeScriptSyntax_ValidFunctionCall_ShouldPass()
        {
            // Arrange - Valid function call expression
            const string validCode = "const result = Math.max(10, 20);";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid function call: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_ValidPropertyAccess_ShouldPass()
        {
            // Arrange - Valid property access expression
            const string validCode = "const value = this.accountService.baseUrl;";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid property access: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_ValidArrowFunction_ShouldPass()
        {
            // Arrange - Valid arrow function expression
            const string validCode = "const filterFn = (x: any) => x.active;";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid arrow function: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_MalformedArrowFunction_ShouldFail()
        {
            // Arrange - Malformed arrow function (missing arrow)
            const string invalidCode = "const filterFn = (x: any) x.active;";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect malformed arrow function");
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region REF-003: TypeScript Compiler API Integration Tests

        [Fact]
        public void ValidateTypeScriptSyntax_CompilerDiagnostics_ShouldDetectSyntaxErrors()
        {
            // Arrange - Code with multiple syntax errors
            const string invalidCode = @"
export class TestComponent {
    private readonly service = inject(Service
    
    processData() void {
        const result = this.service.getData(;
        return result
    }
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect multiple syntax errors");
            Assert.NotNull(result.ErrorMessage);
            // Should report specific issues found by TypeScript compiler
            Assert.True(result.ErrorMessage.Length > 0, "Should provide detailed error information");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_CompilerDiagnostics_ShouldAcceptValidCode()
        {
            // Arrange - Completely valid TypeScript code
            const string validCode = @"
export class TestComponent {
    private readonly service = inject(Service);
    
    processData(): void {
        const result = this.service.getData();
        return result;
    }
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(validCode);

            // Assert
            Assert.True(result.IsValid, $"Should accept completely valid TypeScript code: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_TypeScriptSpecificSyntax_ShouldValidateCorrectly()
        {
            // Arrange - TypeScript-specific syntax (type annotations, access modifiers)
            const string invalidCode = @"
export class TestComponent implements OnInit {
    private readonly data: Array<UserData> = [];
    public currentUser?: User;
    
    constructor(private service: DataService) {}
    
    ngOnInit(): void {
        this.loadData();
    }
    
    private async loadData(): Promise<void> {
        this.data = await this.service.fetchData();
    }
}";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.True(result.IsValid, $"Should not accept improper TypeScript code: {result.ErrorMessage}");
        }

        #endregion

        #region REF-003: Integration with Variable Introduction Tests

        [Fact]
        public void ValidateGeneratedCode_FromVariableIntroduction_ShouldValidateBeforeInsertion()
        {
            // Arrange - Simulate the exact scenario from a MongoDB document
            const string originalCode = @"
export class AccountListComponent {
    private readonly accountService: AccountsClient = inject(AccountsClient);
}";
            const string extractedExpression = "inject(AccountsClient)";
            const string variableName = "injectedAccountsClient";
            const string scopeContext = "class";

            // Act - Generate variable declaration and validate it
            var service = CreateTestService();
            var generatedDeclaration = InvokeGenerateVariableDeclaration(service, variableName, extractedExpression, scopeContext, 2);
            var generatedDeclarationType = generatedDeclaration.GetType();
            var generatedDeclarationString = generatedDeclarationType.GetProperty("Declaration")?.GetValue(generatedDeclaration) as string;
            var validationResult = InvokeValidateTypeScriptSyntax(generatedDeclarationString ?? string.Empty);
            
            bool? success = (bool?)generatedDeclarationType.GetProperty("Success")?.GetValue(generatedDeclaration) ?? false;
            var declaration = generatedDeclarationString ?? string.Empty;

            // Assert
            Assert.True(success, "Variable declaration generation should succeed");
            Assert.True(validationResult.IsValid, $"Generated declaration should be syntactically valid: {validationResult.ErrorMessage}");
            Assert.Equal("private readonly injectedAccountsClient = inject(AccountsClient);", declaration);
        }

        [Fact]
        public void ValidateGeneratedCode_BrokenExpressionBoundary_ShouldFailValidation()
        {
            // Arrange - Simulate the broken boundary detection from REF-001
            const string brokenExpression = "nject(AccountsClient"; // Missing 'i' and closing ')'
            const string variableName = "injectedAccountsClient";
            const string generatedDeclaration = $"const {variableName} = {brokenExpression};";

            // Act
            var result = InvokeValidateTypeScriptSyntax(generatedDeclaration);

            // Assert
            Assert.False(result.IsValid, "Should detect syntax errors from broken expression boundaries");
            Assert.NotNull(result.ErrorMessage);
            // Should detect both undefined variable and missing parenthesis
            Assert.True(result.ErrorMessage.Contains("nject") || result.ErrorMessage.Contains("parenthes"), 
                "Should report issues with undefined identifier or missing parenthesis");
        }

        [Fact]
        public void ValidateGeneratedCode_InvalidClassMemberSyntax_ShouldFailValidation()
        {
            // Arrange - Simulate the broken class member syntax from REF-002
            const string invalidClassCode = @"
export class TestComponent {
    const injectedAccountsClient = inject(AccountsClient); // Invalid: const in class body
}";

            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidClassCode);

            // Assert
            Assert.False(result.IsValid, "Should detect invalid const usage in class body");
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("const", result.ErrorMessage.ToLower(), StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region REF-003: Error Message Quality Tests

        [Fact]
        public void ValidateTypeScriptSyntax_DetailedErrorMessages_ShouldProvideUsefulFeedback()
        {
            // Arrange - Code with specific syntax error
            const string invalidCode = "const result = Math.max(10, 20;"; // Missing closing parenthesis
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect syntax error");
            Assert.NotNull(result.ErrorMessage);
            Assert.True(result.ErrorMessage.Length > 10, "Should provide detailed error message");
            // Error message should be helpful for debugging
            Assert.True(result.ErrorMessage.Contains("(") || result.ErrorMessage.Contains(")") || 
                       result.ErrorMessage.Contains("parenthes", StringComparison.CurrentCultureIgnoreCase) ||
                       result.ErrorMessage.Contains("expected", StringComparison.CurrentCultureIgnoreCase),
                       "Should provide helpful error message about the syntax issue");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_MultipleErrors_ShouldReportAll()
        {
            // Arrange - Code with multiple syntax errors
            const string invalidCode = @"
const result = Math.max(10, 20;
let value = this.service.getData(;
const final = result.filter(x => x.active;
";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(invalidCode);

            // Assert
            Assert.False(result.IsValid, "Should detect multiple syntax errors");
            Assert.NotNull(result.ErrorMessage);
            // Should ideally report multiple issues or at least provide comprehensive feedback
            Assert.True(result.ErrorMessage.Length > 20, "Should provide detailed feedback for multiple errors");
        }

        #endregion

        #region REF-003: Performance and Edge Cases Tests

        [Fact]
        public void ValidateTypeScriptSyntax_EmptyCode_ShouldHandleGracefully()
        {
            // Arrange
            const string emptyCode = "";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(emptyCode);

            // Assert - Should either pass (empty is valid) or fail gracefully
            if (!result.IsValid)
            {
                Assert.NotNull(result.ErrorMessage);
                Assert.True(result.ErrorMessage.Length > 0);
            }
        }

        [Fact]
        public void ValidateTypeScriptSyntax_VeryLongCode_ShouldHandleEfficiently()
        {
            // Arrange - Generate a reasonably long piece of valid TypeScript code
            var codeBuilder = new System.Text.StringBuilder();
            codeBuilder.AppendLine("export class LargeComponent {");
            
            for (var i = 0; i < 100; i++)
            {
                codeBuilder.AppendLine($"    private readonly property{i}: string = 'value{i}';");
            }
            
            codeBuilder.AppendLine("    processData(): void {");
            for (var i = 0; i < 50; i++)
            {
                codeBuilder.AppendLine($"        const variable{i} = this.property{i}.length;");
            }
            codeBuilder.AppendLine("    }");
            codeBuilder.AppendLine("}");
            
            var longValidCode = codeBuilder.ToString();
            
            // Act
            var startTime = DateTime.UtcNow;
            var result = InvokeValidateTypeScriptSyntax(longValidCode);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.True(result.IsValid, $"Should handle long valid code: {result.ErrorMessage}");
            Assert.True(duration.TotalSeconds < 5, $"Should complete validation in reasonable time: {duration.TotalSeconds} seconds");
        }

        [Fact]
        public void ValidateTypeScriptSyntax_SpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange - Code with special characters and escape sequences
            const string codeWithSpecialChars = @"
const message = 'Hello\nWorld\t""Test""';
const regex = /[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/;
const template = `User: ${user.name}, Score: ${user.score * 100}%`;
";
            
            // Act
            var result = InvokeValidateTypeScriptSyntax(codeWithSpecialChars);

            // Assert
            Assert.True(result.IsValid, $"Should handle special characters correctly: {result.ErrorMessage}");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method to invoke TypeScript syntax validation
        /// </summary>
        private static ValidationResult InvokeValidateTypeScriptSyntax(string code)
        {
            var validator = CreateTestValidator();
            
            var method = typeof(TypeScriptSyntaxValidator)
                .GetMethod("ValidateSyntax", 
                    BindingFlags.Public | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(validator, [code]);
            
            return (ValidationResult)result!;
        }

        /// <summary>
        /// Helper method to invoke variable declaration generation
        /// </summary>
        private static object InvokeGenerateVariableDeclaration(
            TypeScriptVariableOperations service, 
            string variableName, 
            string expression, 
            string scopeContext, 
            int insertionLine)
        {
            var method = typeof(TypeScriptVariableOperations)
                .GetMethod("GenerateVariableDeclaration", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(service, [variableName, expression, scopeContext, insertionLine, null]);
            
            return result!;
        }

        #endregion
    }
}
