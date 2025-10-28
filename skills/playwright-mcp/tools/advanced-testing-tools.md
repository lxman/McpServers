# Advanced Testing Tools

Security testing, test data generation, and business rule validation.

## Key Methods

### InjectSecurityPayloads
Inject security test payloads (XSS, SQL injection).

**Parameters:**
- `selector` (string, required): Target field selector
- `payloadType` (string, required): Payload type - xss, sql_injection, script_injection, html_injection
- `sessionId` (string, default: "default"): Session ID

---

### GenerateTestData
Generate test data for forms.

**Parameters:**
- `dataType` (string, required): Data type - person, address, company, ssn, email, phone
- `count` (int, default: 1): Count of records to generate

**Returns:** string - JSON with generated test data

---

### ValidateBusinessRules
Validate form data against business rules.

**Parameters:**
- `formDataJson` (string, required): Form data as JSON
- `validationRules` (string, default: "all"): Validation rules to apply

**Returns:** string - JSON with validation results

---

### GeneratePdf
Generate PDF of current page.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `path` (string?, optional): Optional output path (must be canonical)
- `landscape` (bool, default: false): Use landscape orientation

**Returns:** string - Path to generated PDF

---

### StartVideoRecording
Start video recording of test session.

**Parameters:**
- `sessionId`, `filename`

---

### GenerateTestReport
Generate comprehensive test report.

**Parameters:**
- `testSessionData` (string, required): Test session data
- `format` (string, default: "html"): Report format - html, json, markdown
- `filename` (string, default: "test-report"): Output filename

**Returns:** string - Path to generated report
