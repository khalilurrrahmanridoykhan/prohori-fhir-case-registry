.PHONY: verify

# Requires .NET 8, Node 22+ and npm. No FHIR server or identity provider needed.
verify:
	dotnet test --filter 'Category!=Integration'
	cd web && npm ci && npm run build && npx playwright install chromium && npm test
