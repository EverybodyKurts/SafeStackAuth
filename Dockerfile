FROM mcr.microsoft.com/devcontainers/dotnet:8.0

ENV USER="vscode" \
    WORKDIR="/app"

WORKDIR ${WORKDIR}

COPY --chown=${USER}:${USER} . ${WORKDIR}

RUN chmod +x .devcontainer/scripts/node-debian.sh && \
    ./.devcontainer/scripts/node-debian.sh

RUN dotnet tool restore && \
    dotnet paket restore && \
    dotnet restore SafeStackAuth.sln

CMD ["dotnet", "run"]