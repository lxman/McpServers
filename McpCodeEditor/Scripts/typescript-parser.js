// TypeScript parser script for Node.js
// This file contains the JavaScript code that will be executed in Node.js to parse TypeScript

const ts = require('typescript');

// Helper functions (defined before the exports)
function extractImportInfo(node) {
    const moduleSpecifier = node.moduleSpecifier ? node.moduleSpecifier.getText().replace(/['"]/g, '') : '';
    const namedImports = [];
    
    if (node.importClause) {
        if (node.importClause.namedBindings) {
            if (ts.isNamedImports(node.importClause.namedBindings)) {
                node.importClause.namedBindings.elements.forEach(element => {
                    namedImports.push({
                        name: element.name.getText(),
                        alias: element.propertyName ? element.propertyName.getText() : null
                    });
                });
            }
        }
    }
    
    return {
        module: moduleSpecifier,
        namedImports: namedImports,
        defaultImport: node.importClause && node.importClause.name ? node.importClause.name.getText() : null,
        isTypeOnly: !!(node.importClause && node.importClause.isTypeOnly)
    };
}

function extractExportInfo(node) {
    return {
        isDefault: node.kind === ts.SyntaxKind.ExportAssignment,
        name: node.name ? node.name.getText() : null
    };
}

function extractClassInfo(node) {
    return {
        name: node.name ? node.name.getText() : '<anonymous>',
        isAbstract: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.AbstractKeyword)),
        members: extractClassMembers(node)
    };
}

function extractClassMembers(node) {
    const members = [];
    
    if (node.members) {
        node.members.forEach(member => {
            if (ts.isPropertyDeclaration(member) || ts.isMethodDeclaration(member)) {
                members.push({
                    name: member.name ? member.name.getText() : '<anonymous>',
                    kind: ts.isPropertyDeclaration(member) ? 'property' : 'method',
                    isStatic: !!(member.modifiers && member.modifiers.some(m => m.kind === ts.SyntaxKind.StaticKeyword)),
                    isPrivate: !!(member.modifiers && member.modifiers.some(m => m.kind === ts.SyntaxKind.PrivateKeyword)),
                    isProtected: !!(member.modifiers && member.modifiers.some(m => m.kind === ts.SyntaxKind.ProtectedKeyword))
                });
            }
        });
    }
    
    return members;
}

function extractFunctionInfo(node) {
    return {
        name: node.name ? node.name.getText() : '<anonymous>',
        parameters: extractParameters(node),
        isAsync: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.AsyncKeyword)),
        isExported: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.ExportKeyword))
    };
}

function extractVariableInfo(node) {
    const variables = [];
    
    if (node.declarationList) {
        node.declarationList.declarations.forEach(decl => {
            variables.push({
                name: decl.name.getText(),
                isConst: node.declarationList.flags & ts.NodeFlags.Const,
                isLet: node.declarationList.flags & ts.NodeFlags.Let,
                hasInitializer: !!decl.initializer
            });
        });
    }
    
    return variables;
}

function extractParameters(node) {
    const parameters = [];
    
    if (node.parameters) {
        node.parameters.forEach(param => {
            parameters.push({
                name: param.name ? param.name.getText() : '<unknown>',
                type: param.type ? param.type.getText() : 'any',
                isOptional: !!param.questionToken,
                hasDefault: !!param.initializer,
                isRest: !!param.dotDotDotToken
            });
        });
    }
    
    return parameters;
}

function extractReturnType(node) {
    if (node.type) {
        return node.type.getText();
    }
    
    // Try to infer from return statements
    let inferredType = 'void';
    
    function findReturns(n) {
        if (ts.isReturnStatement(n) && n.expression) {
            // Simple type inference
            const expr = n.expression;
            if (ts.isStringLiteral(expr)) inferredType = 'string';
            else if (ts.isNumericLiteral(expr)) inferredType = 'number';
            else if (expr.kind === ts.SyntaxKind.TrueKeyword || 
                     expr.kind === ts.SyntaxKind.FalseKeyword) inferredType = 'boolean';
            else if (ts.isObjectLiteralExpression(expr)) inferredType = 'object';
            else if (ts.isArrayLiteralExpression(expr)) inferredType = 'any[]';
            else inferredType = 'any';
        }
        ts.forEachChild(n, findReturns);
    }
    
    if (node.body) {
        findReturns(node.body);
    }
    
    return inferredType;
}

function extractUsedVariables(node) {
    const used = new Set();
    
    function findIdentifiers(n) {
        if (ts.isIdentifier(n)) {
            used.add(n.getText());
        }
        ts.forEachChild(n, findIdentifiers);
    }
    
    if (node.body) {
        findIdentifiers(node.body);
    }
    
    return Array.from(used);
}

function extractModifiedVariables(node) {
    const modified = new Set();
    
    function findAssignments(n) {
        if (ts.isBinaryExpression(n) && n.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
            if (ts.isIdentifier(n.left)) {
                modified.add(n.left.getText());
            }
        } else if (ts.isPostfixUnaryExpression(n) || ts.isPrefixUnaryExpression(n)) {
            if (ts.isIdentifier(n.operand)) {
                modified.add(n.operand.getText());
            }
        }
        ts.forEachChild(n, findAssignments);
    }
    
    if (node.body) {
        findAssignments(node.body);
    }
    
    return Array.from(modified);
}

function extractThisReferences(node) {
    const thisRefs = [];
    
    function findThis(n) {
        if (n.kind === ts.SyntaxKind.ThisKeyword) {
            const parent = n.parent;
            if (ts.isPropertyAccessExpression(parent)) {
                thisRefs.push({
                    property: parent.name.getText(),
                    isMethodCall: ts.isCallExpression(parent.parent)
                });
            }
        }
        ts.forEachChild(n, findThis);
    }
    
    if (node.body) {
        findThis(node.body);
    }
    
    return thisRefs;
}

function hasReturnStatement(node) {
    let hasReturn = false;
    
    function findReturn(n) {
        if (ts.isReturnStatement(n)) {
            hasReturn = true;
        }
        if (!hasReturn) {
            ts.forEachChild(n, findReturn);
        }
    }
    
    if (node.body) {
        findReturn(node.body);
    }
    
    return hasReturn;
}

function hasAwaitExpression(node) {
    let hasAwait = false;
    
    function findAwait(n) {
        if (ts.isAwaitExpression(n)) {
            hasAwait = true;
        }
        if (!hasAwait) {
            ts.forEachChild(n, findAwait);
        }
    }
    
    if (node.body) {
        findAwait(node.body);
    }
    
    return hasAwait;
}

function calculateComplexity(node) {
    let complexity = 1;
    
    function countComplexity(n) {
        switch (n.kind) {
            case ts.SyntaxKind.IfStatement:
            case ts.SyntaxKind.ForStatement:
            case ts.SyntaxKind.ForInStatement:
            case ts.SyntaxKind.ForOfStatement:
            case ts.SyntaxKind.WhileStatement:
            case ts.SyntaxKind.DoStatement:
            case ts.SyntaxKind.CaseClause:
            case ts.SyntaxKind.ConditionalExpression:
            case ts.SyntaxKind.CatchClause:
                complexity++;
                break;
        }
        ts.forEachChild(n, countComplexity);
    }
    
    if (node.body) {
        countComplexity(node.body);
    }
    
    return complexity;
}

function extractLocalVariables(node, variables) {
    function findVariables(n) {
        if (ts.isVariableStatement(n)) {
            n.declarationList.declarations.forEach(decl => {
                variables.push({
                    name: decl.name.getText(),
                    kind: n.declarationList.flags & ts.NodeFlags.Const ? 'const' : 
                          n.declarationList.flags & ts.NodeFlags.Let ? 'let' : 'var'
                });
            });
        }
        ts.forEachChild(n, findVariables);
    }
    
    findVariables(node);
}

function extractHeritage(heritageClauses) {
    const heritage = [];
    
    heritageClauses.forEach(clause => {
        clause.types.forEach(type => {
            heritage.push(type.expression.getText());
        });
    });
    
    return heritage;
}

function findClosureVariables(sourceFile, startLine, endLine, localVars) {
    const localVarNames = new Set(localVars.map(v => v.name));
    const usedVars = new Set();
    const definedOutside = new Set();
    
    // Find all variables defined outside our range
    function findOuterVariables(node) {
        const start = sourceFile.getLineAndCharacterOfPosition(node.getStart());
        const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd());
        
        if (start.line + 1 < startLine || end.line + 1 > endLine) {
            if (ts.isVariableStatement(node)) {
                node.declarationList.declarations.forEach(decl => {
                    definedOutside.add(decl.name.getText());
                });
            }
        }
        
        ts.forEachChild(node, findOuterVariables);
    }
    
    // Find all variables used in our range
    function findUsedInRange(node) {
        const start = sourceFile.getLineAndCharacterOfPosition(node.getStart());
        const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd());
        
        if (start.line + 1 >= startLine && end.line + 1 <= endLine) {
            if (ts.isIdentifier(node)) {
                const name = node.getText();
                if (!localVarNames.has(name)) {
                    usedVars.add(name);
                }
            }
        }
        
        ts.forEachChild(node, findUsedInRange);
    }
    
    findOuterVariables(sourceFile);
    findUsedInRange(sourceFile);
    
    // Closure variables are those used but defined outside
    const closureVars = [];
    usedVars.forEach(varName => {
        if (definedOutside.has(varName)) {
            closureVars.push(varName);
        }
    });
    
    return closureVars;
}

// Main parsing function - using exports instead of module.exports for better compatibility
exports.parseTypeScript = function(sourceCode, fileName) {
    // Ensure sourceCode is a string
    if (typeof sourceCode !== 'string') {
        if (sourceCode && sourceCode.toString) {
            sourceCode = sourceCode.toString();
        } else {
            throw new Error(`sourceCode must be a string, received: ${typeof sourceCode}`);
        }
    }
    
    const sourceFile = ts.createSourceFile(
        fileName,
        sourceCode,
        ts.ScriptTarget.Latest,
        true
    );
    
    const nodes = [];
    const imports = [];
    const exports = [];
    const classes = [];
    const functions = [];
    const variables = [];
    
    function visit(node) {
        const nodeInfo = {
            kind: ts.SyntaxKind[node.kind],
            kindValue: node.kind,
            text: node.getText ? node.getText() : '',
            start: sourceFile.getLineAndCharacterOfPosition(node.getStart()),
            end: sourceFile.getLineAndCharacterOfPosition(node.getEnd()),
            children: []
        };
        
        // Categorize nodes
        switch (node.kind) {
            case ts.SyntaxKind.ImportDeclaration:
                imports.push(extractImportInfo(node));
                break;
            case ts.SyntaxKind.ExportDeclaration:
            case ts.SyntaxKind.ExportAssignment:
                exports.push(extractExportInfo(node));
                break;
            case ts.SyntaxKind.ClassDeclaration:
                classes.push(extractClassInfo(node));
                break;
            case ts.SyntaxKind.FunctionDeclaration:
            case ts.SyntaxKind.ArrowFunction:
            case ts.SyntaxKind.FunctionExpression:
                functions.push(extractFunctionInfo(node));
                break;
            case ts.SyntaxKind.VariableStatement:
                variables.push(...extractVariableInfo(node));
                break;
        }
        
        nodes.push(nodeInfo);
        ts.forEachChild(node, visit);
    }
    
    visit(sourceFile);
    
    return {
        fileName: fileName,
        nodes: nodes,
        imports: imports,
        exports: exports,
        classes: classes,
        functions: functions,
        variables: variables,
        diagnostics: sourceFile.parseDiagnostics || []
    };
};

// Extract detailed method information
exports.extractMethodInfo = function(sourceCode, startLine, endLine, fileName) {
    // Ensure sourceCode is a string
    if (typeof sourceCode !== 'string') {
        if (sourceCode && sourceCode.toString) {
            sourceCode = sourceCode.toString();
        } else {
            throw new Error(`sourceCode must be a string, received: ${typeof sourceCode}`);
        }
    }
    
    const sourceFile = ts.createSourceFile(
        fileName,
        sourceCode,
        ts.ScriptTarget.Latest,
        true
    );
    
    let methodInfo = null;
    
    function findMethodInRange(node) {
        const start = sourceFile.getLineAndCharacterOfPosition(node.getStart());
        const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd());
        
        // Check if this node is within our target range
        if (start.line + 1 >= startLine && end.line + 1 <= endLine) {
            if (ts.isFunctionDeclaration(node) || 
                ts.isMethodDeclaration(node) ||
                ts.isArrowFunction(node) ||
                ts.isFunctionExpression(node)) {
                
                methodInfo = {
                    name: node.name ? node.name.getText() : '<anonymous>',
                    parameters: extractParameters(node),
                    returnType: extractReturnType(node),
                    isAsync: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.AsyncKeyword)),
                    isStatic: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.StaticKeyword)),
                    isPrivate: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.PrivateKeyword)),
                    isProtected: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.ProtectedKeyword)),
                    isPublic: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.PublicKeyword)),
                    usedVariables: extractUsedVariables(node),
                    modifiedVariables: extractModifiedVariables(node),
                    thisReferences: extractThisReferences(node),
                    hasReturnStatement: hasReturnStatement(node),
                    hasAwait: hasAwaitExpression(node),
                    complexity: calculateComplexity(node)
                };
                return;
            }
        }
        
        ts.forEachChild(node, findMethodInRange);
    }
    
    findMethodInRange(sourceFile);
    return methodInfo;
};

// Analyze scope and context
exports.analyzeScope = function(sourceCode, startLine, endLine, fileName) {
    // Ensure sourceCode is a string
    if (typeof sourceCode !== 'string') {
        if (sourceCode && sourceCode.toString) {
            sourceCode = sourceCode.toString();
        } else {
            throw new Error(`sourceCode must be a string, received: ${typeof sourceCode}`);
        }
    }
    
    const sourceFile = ts.createSourceFile(
        fileName,
        sourceCode,
        ts.ScriptTarget.Latest,
        true
    );
    
    const scopeInfo = {
        parentClass: null,
        parentFunction: null,
        localVariables: [],
        closureVariables: [],
        thisContext: null,
        imports: [],
        exports: []
    };
    
    function analyzeNode(node, parent) {
        const start = sourceFile.getLineAndCharacterOfPosition(node.getStart());
        const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd());
        
        // Check if our target range is within this scope
        if (start.line + 1 <= startLine && end.line + 1 >= endLine) {
            if (ts.isClassDeclaration(node) || ts.isClassExpression(node)) {
                scopeInfo.parentClass = {
                    name: node.name ? node.name.getText() : '<anonymous>',
                    members: extractClassMembers(node),
                    extends: node.heritageClauses ? extractHeritage(node.heritageClauses) : null
                };
                scopeInfo.thisContext = 'class';
            } else if (ts.isFunctionDeclaration(node) || ts.isMethodDeclaration(node)) {
                scopeInfo.parentFunction = {
                    name: node.name ? node.name.getText() : '<anonymous>',
                    parameters: extractParameters(node),
                    isAsync: !!(node.modifiers && node.modifiers.some(m => m.kind === ts.SyntaxKind.AsyncKeyword))
                };
                if (ts.isMethodDeclaration(node)) {
                    scopeInfo.thisContext = 'method';
                }
            }
            
            // Extract local variables in scope
            if (ts.isBlock(node) || ts.isSourceFile(node)) {
                extractLocalVariables(node, scopeInfo.localVariables);
            }
        }
        
        ts.forEachChild(node, child => analyzeNode(child, node));
    }
    
    analyzeNode(sourceFile, null);
    
    // Determine closure variables (used but not declared locally)
    scopeInfo.closureVariables = findClosureVariables(
        sourceFile, 
        startLine, 
        endLine, 
        scopeInfo.localVariables
    );
    
    return scopeInfo;
};
