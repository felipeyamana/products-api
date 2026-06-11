FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ProductsApi.sln ./
COPY src/ProductsApi/ProductsApi.csproj src/ProductsApi/
RUN dotnet restore src/ProductsApi/ProductsApi.csproj

COPY . ./
RUN dotnet publish src/ProductsApi/ProductsApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "ProductsApi.dll"]
