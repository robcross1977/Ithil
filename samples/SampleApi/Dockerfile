FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy the projects the SampleApi depends on
COPY src/Ithil.Attributes/ src/Ithil.Attributes/
COPY src/Ithil.Core/ src/Ithil.Core/
COPY src/Ithil.Hosting/ src/Ithil.Hosting/
COPY src/Ithil.SourceGenerator/ src/Ithil.SourceGenerator/
COPY samples/SampleApi/ samples/SampleApi/

WORKDIR /repo/samples/SampleApi
RUN dotnet publish SampleApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
USER app
ENTRYPOINT ["dotnet", "SampleApi.dll"]
