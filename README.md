# ❤️ CC Our Story 情侣空间

<p align="center">
  <strong>把两个人的心动、回忆和约定，写进属于自己的小宇宙</strong>
</p>

<p align="center">
  <a href="https://github.com/keeleycenc/cc-our-story/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/keeleycenc/cc-our-story/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/keeleycenc/cc-our-story/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/keeleycenc/cc-our-story?display_name=tag&sort=semver"></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/github/license/keeleycenc/cc-our-story"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet">
  <img alt="Docker" src="https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white">
</p>

<p align="center">
  <a href="https://keeleycenc.com"><strong>在线体验</strong></a> ·
  <a href="#-本地部署">快速开始</a>
  <a href="https://github.com/keeleycenc/cc-our-story/releases/latest">下载最新版</a> ·
  <a href="https://github.com/keeleycenc/cc-our-story/issues">反馈建议</a>
</p>

[![CC Our Story 首页：深色与浅色主题预览](screenshot.png)](https://keeleycenc.com)

基于 ASP.NET Core Razor Pages、Entity Framework Core 和 SQLite，支持最低1 核 2G 的轻量服务器上 Docker 一键部署

## ✨ 功能

- 首页：展示彼此的信息、相伴时间和最近留下的回忆
- 心动：男女主记录「想你」，路过的访客送上祝福
- 点点滴滴：用图片和文字，按时间轴收藏相恋中的每个瞬间
- 纪念日：记住每一个特别的日子，不错过下一次重逢
- 留言：登录用户和访客都能写下想说的话，也可以彼此回复
- 心意商城：把想为彼此做的事挂成心愿，用心意兑换、由对方履约确认
- Web Push / Email 通知：两条渠道可独立开启或同时使用，TA 的每一刻更新都不会错过
- LLM 氛围组：基于大语言模型的虚拟氛围组功能
- 内容保护：重要的记录可以上锁，私密的纪念日只留给彼此
- 后台：写记录、管纪念日和留言、传图片、改站点设置与账号口令等
- 深浅色：默认跟随系统，也可以手动固定喜欢的配色
- 图片懒加载：封面和正文图片用 WebP 派生资源，结合骨架屏和懒加载
- 图片查看器：Lightbox 跟手滑动与渐进加载，压缩图先显，原图解码无感替换
- 附件存储：默认存在本地，配好参数即可切换到阿里云 OSS
- 内联图标：使用 [Lucide](https://lucide.dev) 图标子集，不依赖任何 CDN
- 心有灵犀：私密资产，通过同步回答、默契测试与专属回忆，见证两个人逐渐靠近的每一次共鸣

## 🧩 权限模型

只有三种身份：

| 身份 | 有账号 | 能做什么 |
| --- | --- | --- |
| 男主 | ✅ | 后台全部功能，看得到草稿和上锁的记录 |
| 女主 | ✅ | 后台全部功能，看得到草稿和上锁的记录 |
| 访客 | ❌ | 已发布的内容、留言、点爱心送祝福等 |

数据库里永远只有两行用户。访客没有账号，靠一串指纹（IP + UA + 站点密钥的哈希）区分，不存原始 IP

## 📂 目录结构

```bash
CC.OurStory/
├─ .github/workflows/          # CI 与打包发布
├─ docker/Dockerfile           # 运行镜像
├─ src/                        # 源代码
│  ├─ OurStory.Core/           # 领域模型、设置契约、通用工具（不依赖任何框架）
│  ├─ OurStory.Data/           # EF Core + SQLite：DbContext、映射、迁移
│  ├─ OurStory.Services/       # 业务服务
│  └─ OurStory.Web/            # ASP.NET Core 站点
│     ├─ Pages/                # 前台页面
│     ├─ Areas/Admin/Pages/    # 后台
│     ├─ Api/                  # 心动与通知接口
│     ├─ Infrastructure/       # 图标、令牌、访客指纹、通知投递等
│     └─ wwwroot/              # 样式脚本、manifest 与 Service Worker
├─ tests/OurStory.Tests/       # 单元测试
├─ compose.yaml                # 一键起站配置文件
├─ Directory.Build.props       # 全解决方案共用的属性与包版本
├─ global.json                 # 锁定的 SDK 大版本
└─ OurStory.sln
```

分层是单向的：`Web → Services → Data → Core`

## 🧪 本地部署

### 需要什么

- [.NET SDK 10](https://dotnet.microsoft.com/download)（`dotnet --version` 应当是 10.x）
- 想用 Docker 的话，装个 Docker Desktop 就够了

### 方式一：dotnet watch（开发时用这个）

```powershell
# 改 `.cshtml`、`.cs`、`wwwroot` 下的样式脚本都会立刻生效，不用停掉重开，浏览器还会自己刷新
dotnet watch --project src/OurStory.Web
```

打开 <http://localhost:5080>。移动端需要访问的话需加 `--lan` 开启局域网监听：

```powershell
# 日志会打印内网地址。可指定端口：--lan 8080。Docker 不受此开关影响
dotnet watch --project src/OurStory.Web --lan
```

第一次启动会自动建库，并创建 `boy` / `girl` 两个账号。**口令是随机生成的，只在启动日志里出现这一次**

```text
warn: OurStory.Web.Infrastructure[0]已创建 Boy 账号：登录名 boy，初始口令 xxxxxxxxxxxxxx —— 这串口令只在这里出现一次，登录后请到后台改掉
```

然后到 <http://localhost:5080/login> 登录，后台在 <http://localhost:5080/admin>

### 方式二：Docker 一键起站

```powershell
Copy-Item .env.example .env
docker compose up -d --build
```

打开 <http://localhost:8080>。两个账号的初始口令是随机生成的，从日志查看：

```bash
docker compose logs web
```

## 🚀 远端部署

### 方式一：Docker（推荐）

把仓库拉到服务器上，照着 [Docker 一键起站](#方式二docker-一键起站) 做一遍就行。数据在名为 `ourstory_data` 的卷里

### 方式二：把发布包拖到目录下

在本机打包：

```powershell
dotnet publish src/OurStory.Web -c Release -o dist
```

服务器上装了 .NET 10 运行时的话，把 `dist/` 整个传上去，然后：

```bash
dotnet OurStory.Web.dll
```

服务器上什么都没装，就打一个自带运行时的包（不需要目标机装任何东西）：

```powershell
dotnet publish src/OurStory.Web -c Release -r linux-x64 --self-contained true -o dist
```

传上去之后 `chmod +x OurStory.Web && ./OurStory.Web` 即可。[Releases](../../releases) 里两种包都有现成的

站点默认监听 5000 端口，想换用 `ASPNETCORE_HTTP_PORTS=8080`。长期跑建议交给 systemd：

```ini
# /etc/systemd/system/ourstory.service
[Unit]
Description=CC Our Story
After=network.target

[Service]
WorkingDirectory=/srv/ourstory
ExecStart=/srv/ourstory/OurStory.Web
Restart=always
RestartSec=5
User=www-data
Environment=ASPNETCORE_HTTP_PORTS=8080

[Install]
WantedBy=multi-user.target
```

`WorkingDirectory` 一定要写：`App_Data` 是相对当前工作目录解析的，不写的话 systemd 会用 `/`，数据库就跑到根目录去了。不想操心这个就加一行 `Environment=OURSTORY_DATA_DIR=/srv/ourstory-data`，写绝对路径

### 升级

**Docker 的话不用管文件**，重新构建镜像就行，数据在卷里不受影响：

```bash
git pull
docker compose up -d --build
```

**发布包的话就是解压覆盖**，但顺序不能乱 —— 必须先停进程：

```bash
systemctl stop ourstory
cp -a /srv/ourstory/App_Data /srv/backup/App_Data-$(date +%F)   # 备份
unzip -o CCOurStory-v1.0.1-linux-x64.zip -d /srv/ourstory
chmod +x /srv/ourstory/OurStory.Web                             # 自带运行时的包才需要
systemctl start ourstory
```

### 放在 Nginx 后面

```nginx
server {
    listen 80;
    server_name our.example.com;

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        keep-alive;
    }
}
```

`X-Forwarded-For` 一定要转发：访客指纹靠它区分不同的人，缺了这个头，所有访客会被算成同一个

## 🔔 通知

本站支持浏览器标准 Web Push 与 SMTP Email 两条通知渠道。两条渠道彼此独立，任一渠道暂时不可用都不会阻止另一条继续投递

**前提**：站点必须使用 HTTPS（本地调试 `http://localhost` 亦可）。反代请配好证书，否则浏览器不会授予通知权限

### Web Push 开通步骤

**需要双方各自操作一遍：**

1. **iOS 强制（Android 和 PC 端跳过此步）**：iPhone/iPad 需先用 Safari 打开站点，点击「分享 → 添加到主屏幕」，之后从主屏幕图标进入
2. 进入「后台 → 通知」，点击「开启通知」，浏览器弹出权限框时选择「允许」
3. 勾选需要接收的通知项，保存

每台设备（手机、平板、电脑）需单独开通，已开通设备统一列出，可随时移除

### Email 开通步骤

1. 站点管理员进入「后台 → 站点设置 → 邮件通知」，填写 SMTP Host、端口、加密方式、账号、密码或授权码、发件地址及站点公开地址并启用服务
2. 双方各自进入「后台 → 通知」，填写自己的接收邮箱，可先发送测试邮件确认投递
3. 在接收渠道中勾选 Email；Topic 偏好会同时作用于 Email 与 Web Push

SMTP 发送基于 MailKit，支持不加密、STARTTLS 与 SSL/TLS，可用于 QQ 邮箱、163、Gmail、Outlook 或自建 SMTP

### 注意事项

- **VAPID 密钥**（站点在浏览器端的身份标识）首次启动自动生成，保存在数据目录的 `ourstory.json` 中。**请勿修改**，否则所有已授权设备将集体失效
- 若配置文件为只读挂载，启动日志会提示通知功能不可用
- 同一浏览器只存一份推送订阅。多账号切换登录时，订阅归属**最后点开启**的账号，另一账号设备列表会清空——属浏览器限制，非站点问题。通知页会标明当前归属。多人需各自收通知请用不同浏览器
- 订阅保留在浏览器中，清空服务端设备记录不会自动退订。如页面提示「服务端已无对应记录」，点击开启重新登记即可，无需手动撤销浏览器权限
- **Chrome 首次读取通知状态可能很慢**（要连 Google 的推送服务，网络不通时能卡一两分钟），Edge 走 Windows 通知服务则很快

## 🫂 氛围组

- 发布「点点滴滴」后，它们会隔几分钟到几小时偶尔留下一两句，也可以回复评论，让情侣空间更有生活气息
- 只要兼容 **OpenAI Responses 协议**就能接入：官方、第三方服务或自建网关都可以，不绑定具体厂商

### 添加角色

进入「后台 → 氛围组 → 添加角色」：

| 配置项 | 说明 |
| --- | --- |
| 名字、头像、人设 | 定义角色是谁、什么性格 |
| 服务地址 | 例如 `https://api.openai.com/v1` |
| 模型、API Key | 配置实际使用的模型服务 |
| 会看图 | 支持时会结合动态图片一起理解 |
| 概率与延迟 | 控制留言、回复概率和随机等待时间 |

可以添加多个角色，每个角色使用不同的模型、服务和人设

### 注意

- 草稿不会发送给模型，上锁内容默认也不会发送
- 图片分析失败或模型不支持图片时，会自动降级为纯文本
- 每个角色不会在同一条记录下反复开新留言，避免刷屏
- 模型超时、限流或调用失败不会影响正常发布和留言
- 延迟任务丢失时，后台巡检会尝试补回近期遗漏的互动

## ❓ 忘记口令

站点没有「找回密码」的页面 —— 只有两个人用，加邮件找回不值当，而且多一个对外入口就多一处能被打的地方。重置放在命令行上：能敲到这条命令的人，本来就已经能读到数据库文件了

先看看有哪些账号：

```bash
# 本地 
dotnet run --project src/OurStory.Web -- --list-accounts 

# 容器 
docker compose exec web dotnet OurStory.Web.dll --list-accounts 

# 服务器 
cd /srv/ourstory && ./OurStory.Web --list-accounts
```

重置成一串随机口令（打印出来，只出现这一次）：

```bash
# 本地
dotnet run --project src/OurStory.Web -- --reset-password boy

# 容器
docker compose exec web dotnet OurStory.Web.dll --reset-password boy

# 服务器
cd /srv/ourstory && ./OurStory.Web --reset-password boy
```

想自己指定就再带一个参数（至少 8 位）：`--new-password '新口令'`。维护命令执行完就退出，**不会**启动站点、不会占端口。`--help` 能看到全部

## 🗄️ 数据库

用 EF Core 迁移，启动时自动执行。改了实体之后生成新迁移：

```bash
dotnet tool restore
dotnet dotnet-ef migrations add 迁移名 --project src/OurStory.Data
```

## 😃 git commit emoji

| emoji | emoji代码 | commit 说明 |
| ----- | ----- | ----- |
| 🎉 | `:tada:` | 初次提交 |
| ✨ | `:sparkles:` | 新功能 |
| ⚡️ | `:zap:` | 性能改善 |
| 🐛 | `:bug:` | 修复 Bug |
| 🚑️ | `:ambulance:` | 紧急修复 Bug |
| 🎨 | `:art:` | 改进代码结构/代码格式 |
| 🚚 | `:truck:` | 移动或重命名文件、目录、命名空间等 |
| 💄 | `:lipstick:` | 更新 UI 和样式文件 |
| 🔥 | `:fire:` | 移除代码或文件 |
| 📝 | `:memo:` | 撰写文档 |
| 🚀 | `:rocket:` | 部署功能 |
| ✅ | `:white_check_mark:` | 添加或更新测试 |
| 🔒️ | `:lock:` | 更新安全相关代码 |
| ⬆️ | `:arrow_up:` | 升级依赖 |
| ⬇️ | `:arrow_down:` | 降级依赖 |
| 🔀 | `:twisted_rightwards_arrows:` | 合并分支 |
| ⏪️ | `:rewind:` | 回退到上一个版本 |
| 🔧 | `:wrench:` | 修改配置文件 |
| 🗑️ | `:wastebasket:` | 删除不再需要的代码或文件 |
| ✏️ | `:pencil2:` | 修正拼写或语法错误 |
| ♻️ | `:recycle:` | 重构代码 |
| 💩 | `:poop:` | 改进的(屎)坏(山)代码 |
| 👻 | `:ghost:` | 添加或更新 GIF |
| 👷 | `:construction_worker:` | 添加或更新 CI 构建系统 |
| 🥚 | `:egg:` | 添加或更新彩蛋 |
| 🏗️ | `:building_construction:` | 进行体系结构更改/重大重构 |
| 💡 | `:bulb:` | 在源代码中添加或更新注释 |

## 📄 许可证

项目代码使用 [MIT License](LICENSE)
