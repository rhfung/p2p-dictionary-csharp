FROM microsoft/dotnet:2.0-sdk AS baseimg
COPY ./src /src
RUN cd /src && dotnet publish ./p2pd/p2pd.csproj --configuration Release  --output /output

FROM microsoft/dotnet:2.0-runtime
RUN mkdir /output
COPY --from=baseimg /output /output
WORKDIR /output
EXPOSE 8765
ENTRYPOINT ["dotnet", "p2pd.dll"]
CMD ["--help"]
