#!/usr/bin/env python3
"""Python Analyzer HTTP Server with auto-generated OpenAPI"""
from flask import Flask, jsonify
from flask_restx import Api, Resource, fields
from flask_cors import CORS
import sys
import os

# Add src to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src'))

from tools import PythonTools

app = Flask(__name__)
CORS(app)

# Configure Flask-RESTX with OpenAPI info
api = Api(
    app,
    version='1.0.0',
    title='Python Analyzer API',
    description='Python code analysis tools with auto-generated OpenAPI documentation',
    doc='/docs',  # Serve Swagger UI at /docs
    prefix='/api/python'
)

py_tools = PythonTools()
SERVER_PORT = 7301

# Add custom /description endpoint that returns JSON spec
@app.route('/description')
def get_description():
    """Return OpenAPI specification as JSON"""
    return jsonify(api.__schema__)

# Define request/response models
analyze_model = api.model('AnalyzeRequest', {
    'code': fields.String(required=True, description='Python code to analyze'),
    'fileName': fields.String(description='Optional file name'),
    'pythonVersion': fields.String(description='Python version (default: auto)')
})

symbols_model = api.model('SymbolsRequest', {
    'code': fields.String(required=True, description='Python code to analyze'),
    'fileName': fields.String(description='Optional file name'),
    'filter': fields.String(description="Filter: 'class', 'function', 'variable', or 'all'")
})

format_model = api.model('FormatRequest', {
    'code': fields.String(required=True, description='Python code to format')
})

metrics_model = api.model('MetricsRequest', {
    'code': fields.String(required=True, description='Python code to analyze'),
    'fileName': fields.String(description='Optional file name')
})

completions_model = api.model('CompletionsRequest', {
    'code': fields.String(required=True, description='Python code'),
    'line': fields.Integer(required=True, description='Line number (1-based)'),
    'column': fields.Integer(required=True, description='Column number (0-based)')
})

autopep8_model = api.model('Autopep8Request', {
    'code': fields.String(required=True, description='Python code to format'),
    'maxLineLength': fields.Integer(description='Maximum line length (default: 79)')
})

@api.route('/analyze')
class AnalyzeCode(Resource):
    @api.doc('analyze_code')
    @api.expect(analyze_model)
    def post(self):
        '''Analyze Python code for errors and warnings'''
        data = api.payload
        result = py_tools.analyze_code(
            code=data['code'],
            file_name=data.get('fileName'),
            python_version=data.get('pythonVersion', 'auto')
        )
        return result

@api.route('/symbols')
class GetSymbols(Resource):
    @api.doc('get_symbols')
    @api.expect(symbols_model)
    def post(self):
        '''Extract symbols (classes, functions, variables) from Python code'''
        data = api.payload
        result = py_tools.get_symbols(
            code=data['code'],
            file_name=data.get('fileName'),
            filter=data.get('filter')
        )
        return result

@api.route('/format')
class FormatCode(Resource):
    @api.doc('format_code')
    @api.expect(format_model)
    def post(self):
        '''Format Python code using black formatter'''
        data = api.payload
        result = py_tools.format_code(code=data['code'])
        return result

@api.route('/metrics')
class CalculateMetrics(Resource):
    @api.doc('calculate_metrics')
    @api.expect(metrics_model)
    def post(self):
        '''Calculate code metrics including cyclomatic complexity'''
        data = api.payload
        result = py_tools.calculate_metrics(
            code=data['code'],
            file_name=data.get('fileName')
        )
        return result

@api.route('/type-check')
class TypeCheck(Resource):
    @api.doc('type_check')
    @api.expect(metrics_model)
    def post(self):
        '''Run static type checking using mypy'''
        data = api.payload
        result = py_tools.type_check(
            code=data['code'],
            file_name=data.get('fileName')
        )
        return result

@api.route('/detect-dead-code')
class DetectDeadCode(Resource):
    @api.doc('detect_dead_code')
    @api.expect(metrics_model)
    def post(self):
        '''Detect unused functions, classes, and variables using vulture'''
        data = api.payload
        result = py_tools.detect_dead_code(
            code=data['code'],
            file_name=data.get('fileName')
        )
        return result

@api.route('/lint')
class ComprehensiveLint(Resource):
    @api.doc('comprehensive_lint')
    @api.expect(metrics_model)
    def post(self):
        '''Run comprehensive linting using pylint'''
        data = api.payload
        result = py_tools.comprehensive_lint(
            code=data['code'],
            file_name=data.get('fileName')
        )
        return result

@api.route('/completions')
class GetCompletions(Resource):
    @api.doc('get_completions')
    @api.expect(completions_model)
    def post(self):
        '''Get code completions at a specific position using jedi'''
        data = api.payload
        result = py_tools.get_completions(
            code=data['code'],
            line=data['line'],
            column=data['column']
        )
        return result

@api.route('/format-autopep8')
class FormatAutopep8(Resource):
    @api.doc('format_autopep8')
    @api.expect(autopep8_model)
    def post(self):
        '''Format Python code using autopep8 as an alternative to black'''
        data = api.payload
        result = py_tools.format_with_autopep8(
            code=data['code'],
            max_line_length=data.get('maxLineLength')
        )
        return result

if __name__ == '__main__':
    print(f"Python Analyzer HTTP Server starting on port {SERVER_PORT}")
    print(f"OpenAPI documentation available at: http://localhost:{SERVER_PORT}/description")
    print(f"Swagger UI available at: http://localhost:{SERVER_PORT}/docs")
    app.run(host='0.0.0.0', port=SERVER_PORT, debug=False)
