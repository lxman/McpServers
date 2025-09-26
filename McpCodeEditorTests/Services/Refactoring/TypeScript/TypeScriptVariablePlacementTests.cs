using System.Reflection;
using McpCodeEditor.Services.Refactoring.TypeScript;
using Microsoft.Extensions.Logging;
using Moq;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Services.Validation;
using McpCodeEditor.Services.Analysis;
using McpCodeEditor.Services.TypeScript;

namespace McpCodeEditorTests.Services.Refactoring.TypeScript
{
    /// <summary>
    /// Unit tests for REF-002: Variable Placement Within Class Scope
    /// Tests the scope detection and variable declaration syntax generation in TypeScriptVariableOperations
    /// FIXED: Updated for Phase 2 refactoring - now uses VariableDeclarationGeneratorService directly
    /// </summary>
    public class TypeScriptVariablePlacementTests
    {
        /// <summary>
        /// Creates a VariableDeclarationGeneratorService for testing variable generation
        /// FIXED: No longer uses reflection - directly instantiates the service
        /// </summary>
        private static VariableDeclarationGeneratorService CreateVariableDeclarationService()
        {
            var mockLogger = new Mock<ILogger<VariableDeclarationGeneratorService>>();
            return new VariableDeclarationGeneratorService(mockLogger.Object);
        }

        /// <summary>
        /// Creates a TypeScriptVariableOperations service for tests that still need it
        /// Updated for Phase 2 refactoring - includes all required services
        /// </summary>
        private static TypeScriptVariableOperations CreateTestService()
        {
            var mockLogger = new Mock<ILogger<TypeScriptVariableOperations>>();
            var mockPathValidation = new Mock<IPathValidationService>();
            var mockSyntaxValidator = new Mock<ITypeScriptSyntaxValidator>();

            // Create actual service instances for services that have constructor parameters
            var mockAnalysisServiceLogger = new Mock<ILogger<TypeScriptAnalysisService>>();
            var analysisService = new TypeScriptAnalysisService(mockAnalysisServiceLogger.Object);

            var mockValidatorLogger = new Mock<ILogger<TypeScriptExtractMethodValidator>>();
            var validator = new TypeScriptExtractMethodValidator(mockValidatorLogger.Object, analysisService);

            var mockScopeAnalyzerLogger = new Mock<ILogger<TypeScriptScopeAnalyzer>>();
            var scopeAnalyzer = new TypeScriptScopeAnalyzer(mockScopeAnalyzerLogger.Object);

            // Create real services for Phase 2 refactored functionality
            var boundaryDetectionService = new ExpressionBoundaryDetectionService(new Mock<ILogger<ExpressionBoundaryDetectionService>>().Object);
            VariableDeclarationGeneratorService variableDeclarationService = CreateVariableDeclarationService();
            var mockAstAnalysis = new Mock<ITypeScriptAstAnalysisService>();
            var mockCodeModification = new Mock<ITypeScriptCodeModificationService>();

            return new TypeScriptVariableOperations(
                mockLogger.Object,
                mockPathValidation.Object,
                validator,
                scopeAnalyzer,
                mockSyntaxValidator.Object,
                boundaryDetectionService,
                variableDeclarationService,
                mockAstAnalysis.Object,
                mockCodeModification.Object
            );
        }

        #region REF-002: Class Scope Variable Declaration Tests

        [Fact]
        public void GenerateVariableDeclaration_ClassScope_ShouldUsePrivateReadonly()
        {
            // Arrange - This is the exact scenario from MongoDB document
            const string variableName = "injectedAccountsClient";
            const string expression = "inject(AccountsClient)";
            const string scopeContext = "class";
            const int insertionLine = 10;

            // Expected: Should generate "private readonly injectedAccountsClient = inject(AccountsClient);"
            // Actual (broken): Generates "const injectedAccountsClient = inject(AccountsClient);" - invalid for class members

            // Act
            TestVariableDeclarationResult result = InvokeGenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine);

            // Assert
            Assert.True(result.Success, $"Variable declaration generation failed: {result.ErrorMessage}");
            Assert.Equal("private readonly injectedAccountsClient = inject(AccountsClient);", result.Declaration);
            Assert.Equal("ClassMember", result.DeclarationType);
            Assert.DoesNotContain("const", result.Declaration); // Should NOT use const for class members
            Assert.Contains("private readonly", result.Declaration); // Should use proper class member syntax
        }

        [Fact]
        public void GenerateVariableDeclaration_ClassScope_WithTypeAnnotation_ShouldIncludeType()
        {
            // Arrange
            const string variableName = "accountService";
            const string expression = "inject(AccountsClient)";
            const string scopeContext = "class";
            const int insertionLine = 5;
            const string typeAnnotation = "AccountsClient";

            // Act
            TestVariableDeclarationResult result = InvokeGenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine, typeAnnotation);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("private readonly accountService: AccountsClient = inject(AccountsClient);", result.Declaration);
            Assert.Equal("ClassMember", result.DeclarationType);
        }

        [Fact]
        public void GenerateVariableDeclaration_ClassScope_ComplexExpression_ShouldHandleCorrectly()
        {
            // Arrange
            const string variableName = "calculatedValue";
            const string expression = "Math.max(this.data.length, 10)";
            const string scopeContext = "class";
            const int insertionLine = 15;

            // Act
            TestVariableDeclarationResult result = InvokeGenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("private readonly calculatedValue = Math.max(this.data.length, 10);", result.Declaration); 
            Assert.Equal("ClassMember", result.DeclarationType);
        }

        #endregion

        #region REF-002: Method Scope Variable Declaration Tests

        [Fact]
        public void GenerateVariableDeclaration_MethodScope_ShouldUseConst()
        {
            // Arrange
            const string variableName = "result";
            const string expression = "this.calculate()";
            const string scopeContext = "method";
            const int insertionLine = 20;

            // Act
            TestVariableDeclarationResult result = InvokeGenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("const result = this.calculate();", result.Declaration);
            Assert.Equal("LocalVariable", result.DeclarationType);
            Assert.Contains("const", result.Declaration); // Should use const for method-local variables
            Assert.DoesNotContain("private", result.Declaration); // Should NOT use private for method variables
        }

        [Fact]
        public void GenerateVariableDeclaration_MethodScope_WithTypeAnnotation_ShouldIncludeType()
        {
            // Arrange
            const string variableName = "userList";
            const string expression = "this.service.getUsers()";
            const string scopeContext = "method";
            const int insertionLine = 25;
            const string typeAnnotation = "User[]";

            // Act
            TestVariableDeclarationResult result = InvokeGenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine, typeAnnotation);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("const userList: User[] = this.service.getUsers();", result.Declaration);
            Assert.Equal("LocalVariable", result.DeclarationType);
        }

        #endregion

        #region REF-002: Constructor Scope Variable Declaration Tests

        [Fact]
        public void GenerateVariableDeclaration_ConstructorScope_ShouldUseConstWithThisAssignment()
        {
            // Arrange
            const string variableName = "initializedService";
            const string expression = "new DataService()";
            const string scopeContext = "constructor";
            const int insertionLine = 12;

            // Act
            TestVariableDeclarationResult result = InvokeGenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("const initializedService = new DataService();", result.Declaration);
            Assert.Equal("ConstructorVariable", result.DeclarationType);
            // Note: For constructor scope, we might want to suggest making it a class property instead
        }

        [Fact]
        public void GenerateVariableDeclaration_ConstructorScope_ShouldSuggestClassProperty()
        {
            // Arrange
            const string variableName = "injectedService";
            const string expression = "inject(Service)";
            const string scopeContext = "constructor";
            const int insertionLine = 8;

            // Act
            TestVariableDeclarationResult result = InvokeGenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.SuggestClassProperty, "Should suggest making constructor variables into class properties");
            Assert.Equal("const injectedService = inject(Service);", result.Declaration);
            Assert.Equal("ConstructorVariable", result.DeclarationType);
        }

        #endregion

        #region REF-002: Syntax Validation Tests

        [Fact]
        public void ValidateVariableDeclaration_ClassMemberConst_ShouldFail()
        {
            // Arrange - This tests the broken scenario from MongoDB document
            const string invalidDeclaration = "const injectedAccountsClient = nject(AccountsClient;"; // Missing ) and invalid const for class
            const string scopeContext = "class";

            // Act
            TestPathValidationResult result = InvokeValidateVariableDeclaration(invalidDeclaration, scopeContext);

            // Assert
            Assert.False(result.IsValid, "Should detect that 'const' is invalid for class members");
            Assert.Contains("const", result.ErrorMessage.ToLower());
            Assert.Contains("class", result.ErrorMessage.ToLower());
        }

        [Fact]
        public void ValidateVariableDeclaration_ClassMemberPrivateReadonly_ShouldPass()
        {
            // Arrange
            const string validDeclaration = "private readonly injectedAccountsClient = inject(AccountsClient);";
            const string scopeContext = "class";

            // Act
            TestPathValidationResult result = InvokeValidateVariableDeclaration(validDeclaration, scopeContext);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid class member syntax: {result.ErrorMessage}");
        }

        [Fact]    
        public void ValidateVariableDeclaration_MethodLocalConst_ShouldPass()
        {
            // Arrange
            const string validDeclaration = "const result = this.calculate();";
            const string scopeContext = "method";

            // Act
            TestPathValidationResult result = InvokeValidateVariableDeclaration(validDeclaration, scopeContext);

            // Assert
            Assert.True(result.IsValid, $"Should accept valid method-local syntax: {result.ErrorMessage}");
        }

        [Fact]
        public void ValidateVariableDeclaration_MethodLocalPrivate_ShouldFail()
        {
            // Arrange
            const string invalidDeclaration = "private readonly result = this.calculate();";
            const string scopeContext = "method";

            // Act
            TestPathValidationResult result = InvokeValidateVariableDeclaration(invalidDeclaration, scopeContext);

            // Assert
            Assert.False(result.IsValid, "Should detect that 'private readonly' is invalid for method-local variables");
            Assert.Contains("private", result.ErrorMessage.ToLower());
            Assert.Contains("method", result.ErrorMessage.ToLower());
        }

        #endregion

        #region REF-002: Scope Detection Tests

        [Fact]
        public void DetectScope_WithinClassBody_ShouldReturnClass()
        {
            // Arrange
            const string fileContent = @"
export class AccountListComponent {
    private data: any[] = [];
    
    // INSERT VARIABLE HERE - line 5
    
    constructor() {
        this.initialize();
    }
}";
            const int lineNumber = 5; // Within class but outside any method

            // Act
            TestScopeDetectionResult result = InvokeDetectScope(fileContent, lineNumber);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("class", result.ScopeType);
            Assert.Equal("AccountListComponent", result.ClassName);
            Assert.Null(result.MethodName);
        }

        [Fact]
        public void DetectScope_WithinMethod_ShouldReturnMethod()
        {
            // Arrange
            const string fileContent = @"
export class AccountListComponent {
    
    processData(): void {
        // INSERT VARIABLE HERE - line 5
        return;
    }
}";
            const int lineNumber = 5; // Within processData method

            // Act
            TestScopeDetectionResult result = InvokeDetectScope(fileContent, lineNumber);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("method", result.ScopeType);
            Assert.Equal("AccountListComponent", result.ClassName);
            Assert.Equal("processData", result.MethodName);
        }

        [Fact]
        public void DetectScope_WithinConstructor_ShouldReturnConstructor()
        {
            // Arrange
            const string fileContent = @"
export class AccountListComponent {
    
    constructor() {
        // INSERT VARIABLE HERE - line 5
        this.initialize();
    }
}";
            const int lineNumber = 5; // Within constructor

            // Act
            TestScopeDetectionResult result = InvokeDetectScope(fileContent, lineNumber);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("constructor", result.ScopeType);
            Assert.Equal("AccountListComponent", result.ClassName);
            Assert.Equal("constructor", result.MethodName);
        }

        #endregion

        #region REF-002: Integration Tests

        [Fact]
        public void IntroduceVariable_ClassScope_ShouldGenerateValidClassMember()
        {
            // Arrange - Full integration test for the exact MongoDB scenario
            const string originalCode = @"
export class AccountListComponent {
    private readonly accountService: AccountsClient = inject(AccountsClient);
    
    loadData(): void {
        this.accountService.getData();
    }
}";
            const int line = 3;
            const int startColumn = 54; // Points to 'i' in inject
            const int endColumn = 73;   // Points to ')' 
            const string variableName = "injectedAccountsClient";

            // Act - This should extract inject(AccountsClient) into a class member
            TestIntroduceVariableResult result = InvokeIntroduceVariable(originalCode, line, startColumn, endColumn, variableName);

            // Assert
            Assert.True(result.Success, $"Variable introduction failed: {result.ErrorMessage}");
            Assert.Contains("private readonly injectedAccountsClient", result.ModifiedCode);
            Assert.DoesNotContain("const injectedAccountsClient", result.ModifiedCode); // Should NOT use const for class members
            Assert.Contains("= this.injectedAccountsClient", result.ModifiedCode); // Should replace expression with variable reference
            
            // Verify the generated code is syntactically valid TypeScript
            Assert.Contains("inject(AccountsClient)", result.ModifiedCode); // Should have complete expression
        }

        [Fact]
        public void IntroduceVariable_MethodScope_ShouldGenerateValidLocalVariable()
        {
            // Arrange
            const string originalCode = @"
export class AccountListComponent {
    
    processData(): void {
        const result = Math.max(10, 20);
        return result;
    }
}";
            const int line = 5;
            const int startColumn = 24; // Points to 'M' in Math
            const int endColumn = 35;   // Points to ')'
            const string variableName = "maxValue";

            // Act
            TestIntroduceVariableResult result = InvokeIntroduceVariable(originalCode, line, startColumn, endColumn, variableName);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("const maxValue = Math.max(10, 20);", result.ModifiedCode);
            Assert.Contains("const result = maxValue;", result.ModifiedCode); // Should replace expression with variable reference
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method to invoke GenerateVariableDeclaration directly on the service
        /// FIXED: No longer uses reflection - directly calls the service method
        /// </summary>
        private static TestVariableDeclarationResult InvokeGenerateVariableDeclaration(string variableName, string expression, string scopeContext, int insertionLine, string? typeAnnotation = null)
        {
            VariableDeclarationGeneratorService service = CreateVariableDeclarationService();
            
            object result = service.GenerateVariableDeclaration(variableName, expression, scopeContext, insertionLine, typeAnnotation);
            
            // Convert anonymous object to our test result type
            Type resultType = result.GetType();
            return new TestVariableDeclarationResult
            {
                Success = (bool)resultType.GetProperty("Success")!.GetValue(result)!,
                Declaration = (string)resultType.GetProperty("Declaration")!.GetValue(result)!,
                DeclarationType = (string)resultType.GetProperty("DeclarationType")!.GetValue(result)!,
                ErrorMessage = (string?)resultType.GetProperty("ErrorMessage")?.GetValue(result),
                SuggestClassProperty = (bool)resultType.GetProperty("SuggestClassProperty")!.GetValue(result)!
            };
        }

        /// <summary>
        /// Helper method to invoke ValidateVariableDeclaration directly on the service
        /// FIXED: No longer uses reflection - directly calls the service method
        /// </summary>
        private static TestPathValidationResult InvokeValidateVariableDeclaration(string declaration, string scopeContext)
        {
            VariableDeclarationGeneratorService service = CreateVariableDeclarationService();
            
            object result = service.ValidateVariableDeclaration(declaration, scopeContext);
            
            // Convert anonymous object to our test result type
            Type resultType = result.GetType();
            return new TestPathValidationResult
            {
                IsValid = (bool)resultType.GetProperty("IsValid")!.GetValue(result)!,
                ErrorMessage = (string)resultType.GetProperty("ErrorMessage")!.GetValue(result)!,
                Warnings = (List<string>)resultType.GetProperty("Warnings")!.GetValue(result)!,
                Diagnostics = (List<object>)resultType.GetProperty("Diagnostics")!.GetValue(result)!
            };
        }

        /// <summary>
        /// Helper method to invoke the private DetectScope method using reflection
        /// NOTE: Still uses reflection as this method wasn't extracted during Phase 2
        /// </summary>
        private static TestScopeDetectionResult InvokeDetectScope(string fileContent, int lineNumber)
        {
            TypeScriptVariableOperations service = CreateTestService();
            
            MethodInfo? method = typeof(TypeScriptVariableOperations)
                .GetMethod("DetectScope", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            object? result = method.Invoke(service, [fileContent, lineNumber]);
            Assert.NotNull(result);
            
            // Convert anonymous object to our test result type
            Type resultType = result.GetType();
            return new TestScopeDetectionResult
            {
                Success = (bool)resultType.GetProperty("Success")!.GetValue(result)!,
                ScopeType = (string)resultType.GetProperty("ScopeType")!.GetValue(result)!,
                ClassName = (string?)resultType.GetProperty("ClassName")?.GetValue(result),
                MethodName = (string?)resultType.GetProperty("MethodName")?.GetValue(result),
                ErrorMessage = (string?)resultType.GetProperty("ErrorMessage")?.GetValue(result)
            };
        }

        /// <summary>
        /// Helper method to invoke the private IntroduceVariable method using reflection
        /// NOTE: Still uses reflection as this method is still private (public API is async)
        /// </summary>
        private static TestIntroduceVariableResult InvokeIntroduceVariable(string fileContent, int line, int startColumn, int endColumn, string variableName)
        {
            TypeScriptVariableOperations service = CreateTestService();
            
            MethodInfo? method = typeof(TypeScriptVariableOperations)
                .GetMethod("IntroduceVariable", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            object? result = method.Invoke(service, [fileContent, line, startColumn, endColumn, variableName]);
            Assert.NotNull(result);
            
            // Convert anonymous object to our test result type
            Type resultType = result.GetType();
            return new TestIntroduceVariableResult
            {
                Success = (bool)resultType.GetProperty("Success")!.GetValue(result)!,
                ModifiedCode = (string)resultType.GetProperty("ModifiedCode")!.GetValue(result)!,
                ErrorMessage = (string?)resultType.GetProperty("ErrorMessage")?.GetValue(result)
            };
        }

        #endregion
    }

    #region Test Result Types

    /// <summary>
    /// Test-specific result types that match the anonymous objects returned by reflection
    /// </summary>
    public class TestVariableDeclarationResult
    {
        public bool Success { get; set; }
        public string Declaration { get; set; } = string.Empty;
        public string DeclarationType { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public bool SuggestClassProperty { get; set; }
    }

    public class TestPathValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = [];
        public List<object> Diagnostics { get; set; } = [];
    }

    public class TestScopeDetectionResult
    {
        public bool Success { get; set; }
        public string ScopeType { get; set; } = string.Empty;
        public string? ClassName { get; set; }
        public string? MethodName { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class TestIntroduceVariableResult
    {
        public bool Success { get; set; }
        public string ModifiedCode { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
