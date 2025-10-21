using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Playwright.Core.Services;
using PlaywrightServerMcp.Models;

namespace PlaywrightServerMcp.Tools;

/// <summary>
/// Handles Angular component contract testing for validating component inputs, outputs, and interfaces
/// Implements ANG-012 Component Contract Testing
/// Follows SOLID principles with focused responsibility on component contract validation
/// </summary>
[McpServerToolType]
public class AngularComponentContractTesting(PlaywrightSessionManager sessionManager)
{
    private readonly PlaywrightSessionManager _sessionManager = sessionManager;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Validate Angular component contracts including inputs, outputs, and interfaces")]
    public async Task<string> ValidateComponentContracts(
        [Description("Component selector or data-testid to test")] string componentSelector,
        [Description("Validation scope: 'inputs', 'outputs', 'interfaces', or 'all' (default: 'all')")] string validationScope = "all",
        [Description("Include performance testing in validation (default: true)")] bool includePerformanceTesting = true,
        [Description("Generate improvement recommendations (default: true)")] bool generateRecommendations = true,
        [Description("Maximum test execution time in seconds (default: 60)")] int timeoutSeconds = 60,
        [Description("Session ID for browser context")] string sessionId = "default")
    {
        try
        {
            // Validate session exists
            PlaywrightSessionManager.SessionContext? session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new ContractValidationResult
                {
                    Success = false,
                    ComponentSelector = componentSelector,
                    ErrorMessage = $"Session {sessionId} not found"
                }, JsonOptions);
            }

            var result = new ContractValidationResult
            {
                ComponentSelector = componentSelector,
                // Get testing environment information
                Environment = await GetTestingEnvironmentInfo(session)
            };

            if (!result.Environment.AngularDetected)
            {
                result.ErrorMessage = "Angular not detected or not in development mode. Component contract testing requires Angular DevTools API.";
                return JsonSerializer.Serialize(result, JsonOptions);
            }

            // Extract component contract information
            result.ContractInfo = await ExtractComponentContractInfo(session, componentSelector);

            if (string.IsNullOrEmpty(result.ContractInfo.ComponentName))
            {
                result.ErrorMessage = $"Component not found or not accessible with selector: {componentSelector}";
                return JsonSerializer.Serialize(result, JsonOptions);
            }

            result.ComponentName = result.ContractInfo.ComponentName;

            // Perform contract validations based on scope
            if (validationScope == "all" || validationScope.Contains("inputs"))
            {
                result.InputValidations = await ValidateComponentInputs(session, result.ContractInfo, timeoutSeconds);
            }

            if (validationScope == "all" || validationScope.Contains("outputs"))
            {
                result.OutputValidations = await ValidateComponentOutputs(session, result.ContractInfo, timeoutSeconds);
            }

            if (validationScope == "all" || validationScope.Contains("interfaces"))
            {
                result.InterfaceValidations = await ValidateComponentInterfaces(session, result.ContractInfo, timeoutSeconds);
            }

            // Calculate compliance scores
            result.ComplianceScore = CalculateComplianceScore(result);

            // Identify contract violations
            result.Violations = IdentifyContractViolations(result);

            // Generate recommendations if requested
            if (generateRecommendations)
            {
                result.Recommendations = GenerateContractRecommendations(result);
            }

            // Determine overall success
            result.Success = result.Violations.Count(v => v.Severity == "Critical") == 0 &&
                           result.ComplianceScore.OverallScore >= 70;

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorResult = new ContractValidationResult
            {
                Success = false,
                ComponentSelector = componentSelector,
                ErrorMessage = $"Failed to validate component contracts: {ex.Message}"
            };

            return JsonSerializer.Serialize(errorResult, JsonOptions);
        }
    }

    private async Task<TestingEnvironmentInfo> GetTestingEnvironmentInfo(PlaywrightSessionManager.SessionContext session)
    {
        try
        {
            var jsCode = @"
                (() => {
                    const envInfo = {
                        angularDetected: false,
                        angularVersion: '',
                        devToolsAvailable: false,
                        componentTestingSupported: false,
                        signalsSupported: false,
                        standaloneComponentsSupported: false,
                        testingFramework: '',
                        availableTestingLibraries: []
                    };

                    // Check for Angular
                    if (window.ng) {
                        envInfo.angularDetected = true;
                        envInfo.devToolsAvailable = true;
                        
                        // Get Angular version
                        try {
                            const version = window.ng.version?.full || window.ng.getComponent?.(document.body)?.constructor?.ɵcmp?.factory?.toString().match(/Angular v([\d.]+)/)?.[1] || 'Unknown';
                            envInfo.angularVersion = version;
                            
                            // Check version capabilities
                            const versionNum = parseFloat(version);
                            envInfo.signalsSupported = versionNum >= 16;
                            envInfo.standaloneComponentsSupported = versionNum >= 14;
                            envInfo.componentTestingSupported = true;
                        } catch (e) {
                            console.warn('Failed to get Angular version:', e);
                        }
                    } else if (document.querySelector('[ng-version]')) {
                        envInfo.angularDetected = true;
                        envInfo.angularVersion = document.querySelector('[ng-version]')?.getAttribute('ng-version') || 'Unknown';
                        // Production mode - limited testing capabilities
                        envInfo.componentTestingSupported = false;
                    }

                    // Check for testing frameworks
                    if (window.jasmine) {
                        envInfo.testingFramework = 'jasmine';
                        envInfo.availableTestingLibraries.push('jasmine');
                    }
                    if (window.jest) {
                        envInfo.testingFramework = 'jest';
                        envInfo.availableTestingLibraries.push('jest');
                    }
                    if (window.mocha) {
                        envInfo.testingFramework = 'mocha';
                        envInfo.availableTestingLibraries.push('mocha');
                    }

                    // Check for Angular testing utilities
                    if (window.ng?.testing) {
                        envInfo.availableTestingLibraries.push('angular-testing');
                    }

                    return envInfo;
                })();
            ";

            var environmentResult = await session.Page.EvaluateAsync<JsonElement>(jsCode);

            return new TestingEnvironmentInfo
            {
                AngularDetected = environmentResult.TryGetProperty("angularDetected", out JsonElement detected) && detected.GetBoolean(),
                AngularVersion = environmentResult.TryGetProperty("angularVersion", out JsonElement version) ? version.GetString() ?? "" : "",
                DevToolsAvailable = environmentResult.TryGetProperty("devToolsAvailable", out JsonElement devTools) && devTools.GetBoolean(),
                ComponentTestingSupported = environmentResult.TryGetProperty("componentTestingSupported", out JsonElement supported) && supported.GetBoolean(),
                SignalsSupported = environmentResult.TryGetProperty("signalsSupported", out JsonElement signals) && signals.GetBoolean(),
                StandaloneComponentsSupported = environmentResult.TryGetProperty("standaloneComponentsSupported", out JsonElement standalone) && standalone.GetBoolean(),
                TestingFramework = environmentResult.TryGetProperty("testingFramework", out JsonElement framework) ? framework.GetString() ?? "" : "",
                AvailableTestingLibraries = environmentResult.TryGetProperty("availableTestingLibraries", out JsonElement libraries)
                    ? libraries.EnumerateArray().Select(lib => lib.GetString() ?? "").ToList()
                    : []
            };
        }
        catch (Exception ex)
        {
            return new TestingEnvironmentInfo
            {
                AngularDetected = false,
                AngularVersion = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<ComponentContractInfo> ExtractComponentContractInfo(PlaywrightSessionManager.SessionContext session, string componentSelector)
    {
        try
        {
            var jsCode = $@"
                (() => {{
                    const contractInfo = {{
                        componentName: '',
                        componentPath: '',
                        componentSelector: '{componentSelector}',
                        isStandalone: false,
                        inputs: [],
                        outputs: [],
                        publicMethods: [],
                        publicProperties: [],
                        changeDetection: {{
                            strategy: 'Default',
                            usesSignals: false,
                            usesObservables: false,
                            hasImmutableInputs: false,
                            inputDependencies: []
                        }},
                        lifecycle: {{
                            implementedHooks: [],
                            hasOnInit: false,
                            hasOnDestroy: false,
                            hasOnChanges: false,
                            properCleanup: false
                        }}
                    }};

                    let element;
                    
                    // Try to find element by data-testid first
                    if ('{componentSelector}'.startsWith('[data-testid=')) {{
                        const testId = '{componentSelector}'.match(/data-testid=[""']([^""']+)[""']/)?.[1];
                        if (testId) {{
                            element = document.querySelector(`[data-testid=""${{testId}}""]`);
                        }}
                    }}
                    
                    // Fallback to direct selector
                    if (!element) {{
                        element = document.querySelector('{componentSelector}');
                    }}

                    if (!element) {{
                        return contractInfo;
                    }}

                    try {{
                        // Get component instance if Angular DevTools available
                        if (window.ng?.getComponent) {{
                            const componentInstance = window.ng.getComponent(element);
                            if (componentInstance) {{
                                contractInfo.componentName = componentInstance.constructor.name;
                                
                                // Check if standalone component
                                const componentDef = componentInstance.constructor.ɵcmp;
                                if (componentDef) {{
                                    contractInfo.isStandalone = componentDef.standalone === true;
                                    
                                    // Get change detection strategy
                                    contractInfo.changeDetection.strategy = componentDef.onPush === 1 ? 'OnPush' : 'Default';
                                    
                                    // Extract inputs
                                    if (componentDef.inputs) {{
                                        for (const [key, value] of Object.entries(componentDef.inputs)) {{
                                            const inputInfo = {{
                                                name: key,
                                                type: typeof componentInstance[key],
                                                isRequired: false,
                                                hasDefaultValue: componentInstance[key] !== undefined,
                                                defaultValue: componentInstance[key]?.toString() || '',
                                                allowedValues: [],
                                                validationRules: '',
                                                description: '',
                                                hasTransform: false,
                                                transformFunction: ''
                                            }};
                                            contractInfo.inputs.push(inputInfo);
                                        }}
                                    }}
                                    
                                    // Extract outputs
                                    if (componentDef.outputs) {{
                                        for (const [key, value] of Object.entries(componentDef.outputs)) {{
                                            const outputInfo = {{
                                                name: key,
                                                type: 'EventEmitter',
                                                eventType: 'CustomEvent',
                                                isAsync: false,
                                                description: '',
                                                expectedPayloadProperties: [],
                                                triggerConditions: ''
                                            }};
                                            contractInfo.outputs.push(outputInfo);
                                        }}
                                    }}
                                }}
                                
                                // Extract public methods and properties
                                const proto = Object.getPrototypeOf(componentInstance);
                                const allProps = Object.getOwnPropertyNames(proto);
                                
                                for (const propName of allProps) {{
                                    if (propName.startsWith('_') || propName === 'constructor') continue;
                                    
                                    const descriptor = Object.getOwnPropertyDescriptor(proto, propName);
                                    if (descriptor) {{
                                        if (typeof descriptor.value === 'function') {{
                                            contractInfo.publicMethods.push({{
                                                name: propName,
                                                returnType: 'unknown',
                                                parameters: [],
                                                description: '',
                                                isAsync: descriptor.value.constructor.name === 'AsyncFunction',
                                                accessModifier: 'public'
                                            }});
                                        }} else if (descriptor.get || descriptor.set) {{
                                            contractInfo.publicProperties.push({{
                                                name: propName,
                                                type: typeof componentInstance[propName],
                                                isReadonly: !descriptor.set,
                                                hasGetter: !!descriptor.get,
                                                hasSetter: !!descriptor.set,
                                                description: ''
                                            }});
                                        }}
                                    }}
                                }}
                                
                                // Check for lifecycle hooks
                                const lifecycleHooks = ['ngOnInit', 'ngOnDestroy', 'ngOnChanges', 'ngAfterViewInit', 'ngAfterViewChecked', 'ngAfterContentInit', 'ngAfterContentChecked', 'ngDoCheck'];
                                for (const hook of lifecycleHooks) {{
                                    if (typeof componentInstance[hook] === 'function') {{
                                        contractInfo.lifecycle.implementedHooks.push(hook);
                                        
                                        switch (hook) {{
                                            case 'ngOnInit':
                                                contractInfo.lifecycle.hasOnInit = true;
                                                break;
                                            case 'ngOnDestroy':
                                                contractInfo.lifecycle.hasOnDestroy = true;
                                                contractInfo.lifecycle.properCleanup = true; // Assume proper cleanup if ngOnDestroy exists
                                                break;
                                            case 'ngOnChanges':
                                                contractInfo.lifecycle.hasOnChanges = true;
                                                break;
                                        }}
                                    }}
                                }}
                                
                                // Check for signals usage (Angular 16+)
                                if (window.ng.getDirectiveMetadata) {{
                                    try {{
                                        // Look for signal usage in component
                                        const componentString = componentInstance.constructor.toString();
                                        contractInfo.changeDetection.usesSignals = /signal\(/.test(componentString) || /computed\(/.test(componentString) || /effect\(/.test(componentString);
                                    }} catch (e) {{
                                        // Ignore signal detection errors
                                    }}
                                }}
                            }}
                        }} else {{
                            // Fallback for production mode
                            contractInfo.componentName = element.tagName.toLowerCase();
                            
                            // Extract basic information from DOM
                            const ngVersion = document.querySelector('[ng-version]')?.getAttribute('ng-version');
                            if (ngVersion) {{
                                contractInfo.componentPath = `Angular Component (v${{ngVersion}})`;
                            }}
                        }}
                    }} catch (e) {{
                        console.warn('Error extracting component contract info:', e);
                    }}

                    return contractInfo;
                }})();
            ";

            var contractResult = await session.Page.EvaluateAsync<JsonElement>(jsCode);

            return JsonSerializer.Deserialize<ComponentContractInfo>(contractResult.GetRawText()) ?? new ComponentContractInfo();
        }
        catch (Exception ex)
        {
            return new ComponentContractInfo
            {
                ComponentName = $"Error: {ex.Message}",
                ComponentSelector = componentSelector
            };
        }
    }

    private async Task<List<InputValidationResult>> ValidateComponentInputs(PlaywrightSessionManager.SessionContext session, ComponentContractInfo contractInfo, int timeoutSeconds)
    {
        var results = new List<InputValidationResult>();

        foreach (ComponentInput input in contractInfo.Inputs)
        {
            var inputResult = new InputValidationResult
            {
                InputName = input.Name,
                TestCases = []
            };

            try
            {
                // Generate test cases for the input
                List<InputTestCase> testCases = GenerateInputTestCases(input);

                foreach (InputTestCase testCase in testCases)
                {
                    DateTime testStart = DateTime.UtcNow;

                    try
                    {
                        bool validationResult = await ValidateInputTestCase(session, contractInfo, input, testCase);
                        testCase.Passed = validationResult;
                        testCase.ExecutionTime = DateTime.UtcNow - testStart;

                        if (validationResult)
                        {
                            testCase.ActualBehavior = "Input accepted and processed correctly";
                        }
                        else
                        {
                            testCase.ActualBehavior = "Input validation failed or unexpected behavior";
                        }
                    }
                    catch (Exception ex)
                    {
                        testCase.Passed = false;
                        testCase.ErrorMessage = ex.Message;
                        testCase.ExecutionTime = DateTime.UtcNow - testStart;
                    }

                    inputResult.TestCases.Add(testCase);
                }

                // Calculate metrics
                inputResult.Metrics = CalculateValidationMetrics(inputResult.TestCases.Select(tc => new { tc.Passed, tc.ExecutionTime }));
                inputResult.IsValid = inputResult.TestCases.All(tc => tc.Passed);
            }
            catch (Exception ex)
            {
                inputResult.IsValid = false;
                inputResult.ErrorMessage = $"Failed to validate input {input.Name}: {ex.Message}";
            }

            results.Add(inputResult);
        }

        return results;
    }

    private async Task<List<OutputValidationResult>> ValidateComponentOutputs(PlaywrightSessionManager.SessionContext session, ComponentContractInfo contractInfo, int timeoutSeconds)
    {
        var results = new List<OutputValidationResult>();

        foreach (ComponentOutput output in contractInfo.Outputs)
        {
            var outputResult = new OutputValidationResult
            {
                OutputName = output.Name,
                TestCases = []
            };

            try
            {
                // Generate test cases for the output
                List<OutputTestCase> testCases = GenerateOutputTestCases(output);

                foreach (OutputTestCase testCase in testCases)
                {
                    DateTime testStart = DateTime.UtcNow;

                    try
                    {
                        (bool eventEmitted, object? payload, bool payloadValid) validationResult = await ValidateOutputTestCase(session, contractInfo, output, testCase);
                        testCase.EventEmitted = validationResult.eventEmitted;
                        testCase.EventPayload = validationResult.payload;
                        testCase.PayloadValid = validationResult.payloadValid;
                        testCase.ResponseTime = DateTime.UtcNow - testStart;
                    }
                    catch (Exception ex)
                    {
                        testCase.EventEmitted = false;
                        testCase.ErrorMessage = ex.Message;
                        testCase.ResponseTime = DateTime.UtcNow - testStart;
                    }

                    outputResult.TestCases.Add(testCase);
                }

                // Calculate metrics
                outputResult.Metrics = CalculateValidationMetrics(outputResult.TestCases.Select(tc => new { Passed = tc is { EventEmitted: true, PayloadValid: true }, tc.ResponseTime }));
                outputResult.IsValid = outputResult.TestCases.All(tc => tc.EventEmitted);
            }
            catch (Exception ex)
            {
                outputResult.IsValid = false;
                outputResult.ErrorMessage = $"Failed to validate output {output.Name}: {ex.Message}";
            }

            results.Add(outputResult);
        }

        return results;
    }

    private async Task<List<InterfaceValidationResult>> ValidateComponentInterfaces(PlaywrightSessionManager.SessionContext session, ComponentContractInfo contractInfo, int timeoutSeconds)
    {
        var results = new List<InterfaceValidationResult>();

        // Validate public methods
        if (contractInfo.PublicMethods.Any())
        {
            var methodsResult = new InterfaceValidationResult
            {
                InterfaceName = "PublicMethods",
                TestCases = []
            };

            foreach (ComponentMethod method in contractInfo.PublicMethods)
            {
                var testCase = new InterfaceTestCase
                {
                    TestName = $"Method: {method.Name}",
                    MethodName = method.Name,
                    Parameters = []
                };

                DateTime testStart = DateTime.UtcNow;

                try
                {
                    (bool success, object? result, string error) validationResult = await ValidateMethodInterface(session, contractInfo, method);
                    testCase.Passed = validationResult.success;
                    testCase.ActualResult = validationResult.result;
                    testCase.ExpectedResult = "Method callable without errors";
                    testCase.ExecutionTime = DateTime.UtcNow - testStart;

                    if (!validationResult.success)
                    {
                        testCase.ErrorMessage = validationResult.error;
                    }
                }
                catch (Exception ex)
                {
                    testCase.Passed = false;
                    testCase.ErrorMessage = ex.Message;
                    testCase.ExecutionTime = DateTime.UtcNow - testStart;
                }

                methodsResult.TestCases.Add(testCase);
            }

            methodsResult.Metrics = CalculateValidationMetrics(methodsResult.TestCases.Select(tc => new { tc.Passed, tc.ExecutionTime }));
            methodsResult.IsValid = methodsResult.TestCases.All(tc => tc.Passed);
            results.Add(methodsResult);
        }

        return results;
    }

    private List<InputTestCase> GenerateInputTestCases(ComponentInput input)
    {
        var testCases = new List<InputTestCase>();

        // Valid value test
        testCases.Add(new InputTestCase
        {
            TestName = $"Valid {input.Type} value",
            InputValue = GenerateValidValue(input),
            ExpectedBehavior = "Input should be accepted and processed"
        });

        // Invalid type test
        testCases.Add(new InputTestCase
        {
            TestName = "Invalid type value",
            InputValue = GenerateInvalidValue(input),
            ExpectedBehavior = "Input should be rejected or coerced appropriately"
        });

        // Null/undefined test
        if (!input.IsRequired)
        {
            testCases.Add(new InputTestCase
            {
                TestName = "Null/undefined value",
                InputValue = null,
                ExpectedBehavior = "Should use default value or handle gracefully"
            });
        }

        // Boundary tests
        if (input.Type == "number")
        {
            testCases.Add(new InputTestCase
            {
                TestName = "Boundary value - minimum",
                InputValue = int.MinValue,
                ExpectedBehavior = "Should handle extreme values appropriately"
            });

            testCases.Add(new InputTestCase
            {
                TestName = "Boundary value - maximum",
                InputValue = int.MaxValue,
                ExpectedBehavior = "Should handle extreme values appropriately"
            });
        }

        return testCases;
    }

    private List<OutputTestCase> GenerateOutputTestCases(ComponentOutput output)
    {
        var testCases = new List<OutputTestCase>();

        // Basic event emission test
        testCases.Add(new OutputTestCase
        {
            TestName = $"Emit {output.Name} event",
            TriggerAction = "Basic trigger action"
        });

        // Event payload test
        if (output.ExpectedPayloadProperties.Any())
        {
            testCases.Add(new OutputTestCase
            {
                TestName = $"Validate {output.Name} payload",
                TriggerAction = "Trigger with payload validation"
            });
        }

        return testCases;
    }

    private async Task<bool> ValidateInputTestCase(PlaywrightSessionManager.SessionContext session, ComponentContractInfo contractInfo, ComponentInput input, InputTestCase testCase)
    {
        try
        {
            var jsCode = $@"
                (() => {{
                    const element = document.querySelector('{contractInfo.ComponentSelector}');
                    if (!element || !window.ng?.getComponent) {{
                        return false;
                    }}

                    const component = window.ng.getComponent(element);
                    if (!component) {{
                        return false;
                    }}

                    try {{
                        // Set the input value
                        component['{input.Name}'] = {JsonSerializer.Serialize(testCase.InputValue)};
                        
                        // Trigger change detection
                        if (window.ng.applyChanges) {{
                            window.ng.applyChanges(component);
                        }}
                        
                        return true;
                    }} catch (e) {{
                        console.warn('Input validation error:', e);
                        return false;
                    }}
                }})();
            ";

            var result = await session.Page.EvaluateAsync<bool>(jsCode);
            return result;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(bool eventEmitted, object? payload, bool payloadValid)> ValidateOutputTestCase(PlaywrightSessionManager.SessionContext session, ComponentContractInfo contractInfo, ComponentOutput output, OutputTestCase testCase)
    {
        try
        {
            var jsCode = $@"
                (() => {{
                    return new Promise((resolve) => {{
                        const element = document.querySelector('{contractInfo.ComponentSelector}');
                        if (!element) {{
                            resolve({{ eventEmitted: false, payload: null, payloadValid: false }});
                            return;
                        }}

                        let eventCaptured = false;
                        let capturedPayload = null;

                        // Listen for the event
                        const eventListener = (event) => {{
                            eventCaptured = true;
                            capturedPayload = event.detail || event;
                            
                            setTimeout(() => {{
                                resolve({{
                                    eventEmitted: eventCaptured,
                                    payload: capturedPayload,
                                    payloadValid: true // Basic validation - could be enhanced
                                }});
                            }}, 100);
                        }};

                        element.addEventListener('{output.Name}', eventListener);

                        // Try to trigger the event
                        try {{
                            if (window.ng?.getComponent) {{
                                const component = window.ng.getComponent(element);
                                if (component && component['{output.Name}']) {{
                                    // Trigger the output if it's an EventEmitter
                                    if (typeof component['{output.Name}'].emit === 'function') {{
                                        component['{output.Name}'].emit({{ test: true }});
                                    }}
                                }}
                            }}
                        }} catch (e) {{
                            console.warn('Output trigger error:', e);
                        }}

                        // Timeout after 2 seconds
                        setTimeout(() => {{
                            element.removeEventListener('{output.Name}', eventListener);
                            if (!eventCaptured) {{
                                resolve({{ eventEmitted: false, payload: null, payloadValid: false }});
                            }}
                        }}, 2000);
                    }});
                }})();
            ";

            var result = await session.Page.EvaluateAsync<JsonElement>(jsCode);

            bool eventEmitted = result.TryGetProperty("eventEmitted", out JsonElement emitted) && emitted.GetBoolean();
            object? payload = result.TryGetProperty("payload", out JsonElement payloadElement) ? payloadElement : null;
            bool payloadValid = result.TryGetProperty("payloadValid", out JsonElement valid) && valid.GetBoolean();

            return (eventEmitted, payload, payloadValid);
        }
        catch
        {
            return (false, null, false);
        }
    }

    private async Task<(bool success, object? result, string error)> ValidateMethodInterface(PlaywrightSessionManager.SessionContext session, ComponentContractInfo contractInfo, ComponentMethod method)
    {
        try
        {
            var jsCode = $@"
                (() => {{
                    const element = document.querySelector('{contractInfo.ComponentSelector}');
                    if (!element || !window.ng?.getComponent) {{
                        return {{ success: false, result: null, error: 'Component not accessible' }};
                    }}

                    const component = window.ng.getComponent(element);
                    if (!component) {{
                        return {{ success: false, result: null, error: 'Component instance not found' }};
                    }}

                    try {{
                        if (typeof component['{method.Name}'] !== 'function') {{
                            return {{ success: false, result: null, error: 'Method not found or not a function' }};
                        }}

                        // Call the method with minimal parameters
                        const result = component['{method.Name}']();
                        
                        return {{ success: true, result: result, error: '' }};
                    }} catch (e) {{
                        return {{ success: false, result: null, error: e.message }};
                    }}
                }})();
            ";

            var result = await session.Page.EvaluateAsync<JsonElement>(jsCode);

            bool success = result.TryGetProperty("success", out JsonElement successProp) && successProp.GetBoolean();
            object? methodResult = result.TryGetProperty("result", out JsonElement resultProp) ? resultProp : null;
            string error = result.TryGetProperty("error", out JsonElement errorProp) ? errorProp.GetString() ?? "" : "";

            return (success, methodResult, error);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private ValidationMetrics CalculateValidationMetrics(IEnumerable<dynamic> testResults)
    {
        List<dynamic> results = testResults.ToList();
        int totalTests = results.Count;
        int passedTests = results.Count(r => r.Passed);
        TimeSpan totalTime = results.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.ExecutionTime);

        return new ValidationMetrics
        {
            TotalTests = totalTests,
            PassedTests = passedTests,
            FailedTests = totalTests - passedTests,
            TotalExecutionTime = totalTime
        };
    }

    private ContractComplianceScore CalculateComplianceScore(ContractValidationResult result)
    {
        double inputScore = result.InputValidations.Any()
            ? result.InputValidations.Average(iv => iv.Metrics.PassRate)
            : 100;

        double outputScore = result.OutputValidations.Any()
            ? result.OutputValidations.Average(ov => ov.Metrics.PassRate)
            : 100;

        double interfaceScore = result.InterfaceValidations.Any()
            ? result.InterfaceValidations.Average(iv => iv.Metrics.PassRate)
            : 100;

        double typeSafetyScore = CalculateTypeSafetyScore(result);
        double errorHandlingScore = CalculateErrorHandlingScore(result);
        double performanceScore = CalculatePerformanceScore(result);

        double overallScore = (inputScore + outputScore + interfaceScore + typeSafetyScore + errorHandlingScore + performanceScore) / 6;

        return new ContractComplianceScore
        {
            OverallScore = overallScore,
            InputComplianceScore = inputScore,
            OutputComplianceScore = outputScore,
            InterfaceComplianceScore = interfaceScore,
            TypeSafetyScore = typeSafetyScore,
            ErrorHandlingScore = errorHandlingScore,
            PerformanceScore = performanceScore
        };
    }

    private double CalculateTypeSafetyScore(ContractValidationResult result)
    {
        // Basic type safety assessment
        var score = 100.0;

        // Deduct points for inputs without proper typing
        int untypedInputs = result.ContractInfo.Inputs.Count(i => i.Type == "any" || string.IsNullOrEmpty(i.Type));
        score -= untypedInputs * 10;

        // Deduct points for outputs without proper typing
        int untypedOutputs = result.ContractInfo.Outputs.Count(o => string.IsNullOrEmpty(o.Type));
        score -= untypedOutputs * 10;

        return Math.Max(0, score);
    }

    private double CalculateErrorHandlingScore(ContractValidationResult result)
    {
        // Basic error handling assessment
        var score = 100.0;

        // Check if component has proper lifecycle cleanup
        if (!result.ContractInfo.Lifecycle.HasOnDestroy)
        {
            score -= 20;
        }

        // Check for failed validations
        int failedValidations = result.InputValidations.Count(iv => !iv.IsValid) +
                                result.OutputValidations.Count(ov => !ov.IsValid) +
                                result.InterfaceValidations.Count(iv => !iv.IsValid);

        score -= failedValidations * 15;

        return Math.Max(0, score);
    }

    private double CalculatePerformanceScore(ContractValidationResult result)
    {
        // Basic performance assessment
        var score = 100.0;

        // Check for OnPush change detection strategy
        if (result.ContractInfo.ChangeDetection.Strategy == "OnPush")
        {
            score += 10; // Bonus for performance-optimized change detection
        }

        // Assess test execution times
        IEnumerable<TimeSpan> allTestTimes = result.InputValidations.SelectMany(iv => iv.TestCases.Select(tc => tc.ExecutionTime))
                          .Concat(result.OutputValidations.SelectMany(ov => ov.TestCases.Select(tc => tc.ResponseTime)))
                          .Concat(result.InterfaceValidations.SelectMany(iv => iv.TestCases.Select(tc => tc.ExecutionTime)));

        double averageTime = allTestTimes.Any() ? allTestTimes.Average(t => t.TotalMilliseconds) : 0;

        // Deduct points for slow operations (>100ms average)
        if (averageTime > 100)
        {
            score -= Math.Min(30, (averageTime - 100) / 10);
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private List<ContractViolation> IdentifyContractViolations(ContractValidationResult result)
    {
        var violations = new List<ContractViolation>();

        // Check for missing required inputs
        IEnumerable<ComponentInput> requiredInputs = result.ContractInfo.Inputs.Where(i => i.IsRequired);
        foreach (ComponentInput input in requiredInputs)
        {
            InputValidationResult? validation = result.InputValidations.FirstOrDefault(iv => iv.InputName == input.Name);
            if (validation?.IsValid == false)
            {
                violations.Add(new ContractViolation
                {
                    ViolationType = "RequiredInputValidation",
                    Severity = "High",
                    Description = $"Required input '{input.Name}' failed validation",
                    Location = $"Component: {result.ComponentName}, Input: {input.Name}",
                    Recommendation = "Ensure required inputs are properly validated and handled",
                    AffectedElements = { input.Name }
                });
            }
        }

        // Check for missing lifecycle cleanup
        if (!result.ContractInfo.Lifecycle.HasOnDestroy && result.ContractInfo.Lifecycle.ImplementedHooks.Any())
        {
            violations.Add(new ContractViolation
            {
                ViolationType = "LifecycleCleanup",
                Severity = "Medium",
                Description = "Component implements lifecycle hooks but missing ngOnDestroy for cleanup",
                Location = $"Component: {result.ComponentName}",
                Recommendation = "Implement ngOnDestroy for proper resource cleanup",
                AffectedElements = { "ngOnDestroy" }
            });
        }

        // Check for failed interface validations
        foreach (InterfaceValidationResult interfaceValidation in result.InterfaceValidations.Where(iv => !iv.IsValid))
        {
            violations.Add(new ContractViolation
            {
                ViolationType = "InterfaceContract",
                Severity = "Medium",
                Description = $"Interface validation failed for {interfaceValidation.InterfaceName}",
                Location = $"Component: {result.ComponentName}, Interface: {interfaceValidation.InterfaceName}",
                Recommendation = "Review and fix interface implementation",
                AffectedElements = { interfaceValidation.InterfaceName }
            });
        }

        return violations;
    }

    private List<ContractRecommendation> GenerateContractRecommendations(ContractValidationResult result)
    {
        var recommendations = new List<ContractRecommendation>();

        // Recommend OnPush change detection if not used
        if (result.ContractInfo.ChangeDetection.Strategy != "OnPush")
        {
            recommendations.Add(new ContractRecommendation
            {
                Category = "Performance",
                Priority = "Medium",
                Title = "Implement OnPush Change Detection",
                Description = "Component uses default change detection strategy which may impact performance",
                Implementation = "Add ChangeDetectionStrategy.OnPush to component decorator",
                ExpectedBenefit = "Improved performance and reduced unnecessary change detection cycles",
                EstimatedEffort = 2
            });
        }

        // Recommend input validation improvements
        IEnumerable<InputValidationResult> invalidInputs = result.InputValidations.Where(iv => !iv.IsValid);
        if (invalidInputs.Any())
        {
            recommendations.Add(new ContractRecommendation
            {
                Category = "Type Safety",
                Priority = "High",
                Title = "Improve Input Validation",
                Description = $"Some inputs failed validation: {string.Join(", ", invalidInputs.Select(iv => iv.InputName))}",
                Implementation = "Add proper type annotations and validation logic for component inputs",
                ExpectedBenefit = "Better type safety and more robust component behavior",
                EstimatedEffort = 4
            });
        }

        // Recommend signals usage for modern Angular
        if (result.Environment.SignalsSupported && !result.ContractInfo.ChangeDetection.UsesSignals)
        {
            recommendations.Add(new ContractRecommendation
            {
                Category = "Modernization",
                Priority = "Low",
                Title = "Consider Using Angular Signals",
                Description = "Angular Signals are supported but not used in this component",
                Implementation = "Refactor reactive state management to use signals API",
                ExpectedBenefit = "Better reactivity and performance with modern Angular patterns",
                EstimatedEffort = 8
            });
        }

        return recommendations;
    }

    private object? GenerateValidValue(ComponentInput input)
    {
        return input.Type.ToLower() switch
        {
            "string" => "test-value",
            "number" => 42,
            "boolean" => true,
            "object" => new { test = true },
            "array" => new[] { "item1", "item2" },
            _ => "default-value"
        };
    }

    private object? GenerateInvalidValue(ComponentInput input)
    {
        return input.Type.ToLower() switch
        {
            "string" => 123, // Number instead of string
            "number" => "not-a-number", // String instead of number
            "boolean" => "true", // String instead of boolean
            "object" => "not-an-object", // String instead of object
            "array" => "not-an-array", // String instead of array
            _ => new { invalid = true }
        };
    }
}
