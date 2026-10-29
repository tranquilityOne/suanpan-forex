# 采集（Collector，接收采集指令、盯盘）的镜像（后端约定 §14）：多阶段构建，以非 root 用户运行，容器时区为 UTC（NFR-122）。
# 在仓库根目录构建：docker build -f src/Suanpan.Collector/Dockerfile -t suanpan-collector .
# 配置全部通过环境变量 SUANPAN__节__键 注入（后端约定 §12），镜像中不含任何密钥；监听 8080（镜像默认），只用于以后的健康检查。

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先只复制项目文件还原依赖，只改源码时可以复用这一层；新增被引用的项目时在这里加一行
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/Suanpan.Core/Suanpan.Core.csproj src/Suanpan.Core/
COPY src/Suanpan.Shared/Suanpan.Shared.csproj src/Suanpan.Shared/
COPY src/Suanpan.Data/Suanpan.Data.csproj src/Suanpan.Data/
COPY src/Suanpan.Infrastructure/Suanpan.Infrastructure.csproj src/Suanpan.Infrastructure/
COPY src/Suanpan.Services/Suanpan.Services.csproj src/Suanpan.Services/
COPY src/Suanpan.Collector/Suanpan.Collector.csproj src/Suanpan.Collector/
RUN dotnet restore src/Suanpan.Collector/Suanpan.Collector.csproj

COPY src/ src/
RUN dotnet publish src/Suanpan.Collector/Suanpan.Collector.csproj -c Release -o /app --no-restore -p:UseAppHost=false

# 运行阶段：Debian 系的 ASP.NET Core 运行时镜像自带 IANA 时区库（tzdata）和 ICU，排期的时区换算（BR-037）依赖它们
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ENV TZ=UTC
WORKDIR /app
COPY --from=build /app .
# 镜像内置的非 root 用户 app
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Suanpan.Collector.dll"]
