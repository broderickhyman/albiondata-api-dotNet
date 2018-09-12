all:
	dotnet run --project ./albiondata-api-dotNet

release:
	dotnet publish -c Release ./albiondata-api-dotNet
