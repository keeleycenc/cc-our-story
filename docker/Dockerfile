# 这里不写 `# syntax=` 指令：那一行会让 BuildKit 先去 Docker Hub 拉一个前端镜像，
# 国内网络经常连不上 auth.docker.io 而直接构建失败。本文件只用到经典语法，
# 内置前端完全够用，基础镜像走 mcr.microsoft.com，不经过 Docker Hub。

# ---- 构建 ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先只拷贝工程描述文件，依赖没变时这一层能命中缓存
COPY global.json NuGet.Config Directory.Build.props ./
COPY src/OurStory.Core/OurStory.Core.csproj      src/OurStory.Core/
COPY src/OurStory.Data/OurStory.Data.csproj      src/OurStory.Data/
COPY src/OurStory.Services/OurStory.Services.csproj src/OurStory.Services/
COPY src/OurStory.Web/OurStory.Web.csproj        src/OurStory.Web/
RUN dotnet restore src/OurStory.Web/OurStory.Web.csproj

COPY src/ src/
RUN dotnet publish src/OurStory.Web/OurStory.Web.csproj -c Release -o /app --no-restore

# ---- 运行 ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# tzdata：站点时区靠它解析，缺了它 Asia/Shanghai 会静默退回 UTC，
#         「今天」和「相恋第 N 天」就会差上几个小时
# curl：  healthcheck 要用，镜像里默认连 wget 都没有
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app ./

# 数据库、附件、数据保护密钥和站点配置都在 /data 里，只要挂住这一个目录就不会丢东西。
# 先在镜像里建好并交给 app 用户，命名卷初始化时会带上这份属主。
RUN mkdir -p /data && chown -R app:app /data
USER app
VOLUME ["/data"]

# 镜像里只留运行参数：端口和数据目录。站点怎么配，看 /data/ourstory.json
ENV ASPNETCORE_HTTP_PORTS=8080 \
    OURSTORY_DATA_DIR=/data \
    DOTNET_gcServer=0

EXPOSE 8080
ENTRYPOINT ["dotnet", "OurStory.Web.dll"]
