@echo off
cd /d "%~dp0"
echo Starting Go Analyzer HTTP Server on port 7300...
echo Current directory: %cd%
echo.
echo Downloading Go dependencies...
go mod tidy
go mod download
echo.
echo Installing swag CLI if needed...
go install github.com/swaggo/swag/cmd/swag@latest
echo.
echo Generating OpenAPI documentation...
swag init -g http_server.go -o ./docs
echo.
echo Starting server...
go run http_server.go
