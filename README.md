# Maxolla Ollama Manager

> 简洁高效的本地 Ollama 大模型管理工具，支持模型管理、聊天测试、GPU 监控、日志查看、数据备份与还原，深度适配 Windows 系统，开箱即用。

[简体中文](README.md) · [繁體中文](README.md#繁體中文) · [English](README.md#english) · [日本語](README.md#日本語) · [한국어](README.md#한국어) · [Русский](README.md#русский) · [Español](README.md#español) · [العربية](README.md#العربية)

---

## 简体中文

**Maxolla Ollama Manager** 是一款运行在 Windows 上的本地大模型管理桌面应用，用于管理本地部署的 Ollama 实例。它提供了清晰的模型管理界面、实时 GPU 监控、交互式聊天测试、日志查看以及完整的数据备份与还原功能。

### 主要功能

- **模型管理** — 浏览、安装、删除本地 Ollama 模型，查看模型详情与参数配置
- **聊天测试** — 直接在应用中与大模型对话，支持 Markdown 渲染（标题、列表、代码块、引用、链接等），代码块右上角一键复制，操作按钮带即时反馈
- **GPU 监控** — 实时显示 GPU 利用率、显存占用、温度等关键指标
- **运行监控** — 查看当前正在运行的模型会话
- **日志查看** — 实时查看 Ollama 服务端日志，支持关键字过滤
- **数据备份与还原** — 一键备份全部模型数据，支持从备份文件完整还原
- **系统托盘** — 最小化到托盘运行，后台持续监控模型状态
- **深色主题** — 精心设计的深色界面配色，护眼且专业
- **多语言支持** — 支持简体中文、繁体中文、英语、日语、韩语、俄语、西班牙语、阿拉伯语八种语言

### 技术栈

- **.NET 8** + **WPF** — 原生 Windows 桌面框架，性能优异
- **CommunityToolkit.Mvvm** — 现代化 MVVM 架构实现
- **Ollama API** — 与本地 Ollama 服务实时通信

### 快速开始

#### 下载运行（推荐）

前往 [Releases](https://github.com/vipx/Maxolla/releases) 页面，下载最新版压缩包，解压后双击 `MaxollaOllamaManager.exe` 即可运行。无需安装，无需配置环境。

> **系统要求：** Windows 10/11，64 位。需要预先安装并运行 Ollama（[ollama.com](https://ollama.com)）。

#### 从源码构建

```bash
# 克隆仓库
git clone https://github.com/vipx/Maxolla.git

# 还原依赖并构建
dotnet restore
dotnet build --configuration Release

# 发布为单文件 EXE
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# 启动
./publish/v1.1.6/MaxollaOllamaManager.exe
```

### 项目结构

```
Maxolla/
├── src/
│   ├── OllamaManager.App/          # WPF 主应用（视图、视图模型、主题、托盘）
│   ├── OllamaManager.Core/         # 核心接口与模型定义
│   ├── OllamaManager.Infrastructure/# Ollama API 客户端、日志解析、本地化
│   └── OllamaManager.Tests/        # 单元测试
├── icon.png                        # 应用图标
├── LICENSE                         # 署名-非商用-免费分享许可证
└── README.md                       # 本文件
```

> **说明：** 预编译的二进制文件（v1.1.6 单文件 EXE）不存放在源码仓库中，请前往 [Releases](https://github.com/vipx/Maxolla/releases) 页面下载。

---

## 繁體中文

**Maxolla Ollama Manager** 是一款運行在 Windows 上的本地大模型管理桌面應用，用於管理本地部署的 Ollama 實例。它提供了清晰的模型管理介面、即時 GPU 監控、互動式聊天測試、日誌查看以及完整的資料備份與還原功能。

### 主要功能

- **模型管理** — 瀏覽、安裝、刪除本地 Ollama 模型，查看模型詳情與參數配置
- **聊天測試** — 直接在應用中與大模型對話，支援 Markdown 渲染（標題、列表、程式碼區塊、引用、連結等），程式碼區塊右上角一鍵複製，操作按鈕帶即時回饋
- **GPU 監控** — 即時顯示 GPU 利用率、記憶體占用、溫度等關鍵指標
- **運行監控** — 查看目前正在運行的模型對話
- **日誌查看** — 即時查看 Ollama 服務端日誌，支援關鍵字過濾
- **資料備份與還原** — 一鍵備份全部模型資料，支援從備份檔完整還原
- **系統托盤** — 最小化到托盤執行，後台持續監控模型狀態
- **深色主題** — 精心設計的深色介面色調，護眼且專業
- **多語言支援** — 支援繁體中文、簡體中文、英語、日語、韓語、俄語、西班牙語、阿拉伯語八種語言

### 技術架構

- **.NET 8** + **WPF** — 原生 Windows 桌面框架，效能優異
- **CommunityToolkit.Mvvm** — 現代化 MVVM 架構實現
- **Ollama API** — 與本地 Ollama 服務即時通訊

### 快速開始

#### 下載執行（推薦）

前往 [Releases](https://github.com/vipx/Maxolla/releases) 頁面，下載最新版壓縮包，解壓後雙擊 `MaxollaOllamaManager.exe` 即可執行。无需安裝，無需配置環境。

> **系統需求：** Windows 10/11，64 位元。需要預先安裝並執行 Ollama（[ollama.com](https://ollama.com)）。

#### 從原始碼建置

```bash
# 克隆倉庫
git clone https://github.com/vipx/Maxolla.git

# 還原依賴並建置
dotnet restore
dotnet build --configuration Release

# 發佈為單一檔案 EXE
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# 啟動
./publish/v1.1.6/MaxollaOllamaManager.exe
```

歡迎 Fork 並免費分享。基於本專案的衍生作品須遵守授權條款：**標註來源**（含原作者與原專案連結）、**免費分享**（不得設置付費牆或訂閱屏障）、**不得商用**、衍生作品須採用相容授權繼續發佈。學術研究、個人學習、非營利組織內部使用不受商用限制。詳見 [LICENSE](LICENSE)。

---

## English

**Maxolla Ollama Manager** is a Windows desktop application for managing locally deployed Ollama instances. It provides a clean model management interface, real-time GPU monitoring, interactive chat testing, log viewing, and complete data backup/restore functionality.

### Key Features

- **Model Management** — Browse, install, and delete local Ollama models; view model details and parameter configurations
- **Chat Testing** — Chat with local LLMs directly in the app; supports Markdown rendering (headings, lists, code blocks, quotes, links, etc.); one-click copy for code blocks; instant feedback on action buttons
- **GPU Monitoring** — Real-time GPU utilization, memory usage, and temperature display
- **Running Sessions** — View currently active model sessions
- **Log Viewer** — Real-time Ollama server log viewer with keyword filtering
- **Backup & Restore** — One-click backup of all model data; full restore from backup files
- **System Tray** — Minimize to tray for background monitoring
- **Dark Theme** — Carefully crafted dark color scheme for comfortable extended use
- **8 Languages** — Simplified Chinese, Traditional Chinese, English, Japanese, Korean, Russian, Spanish, Arabic

### Tech Stack

- **.NET 8** + **WPF** — Native Windows desktop framework, excellent performance
- **CommunityToolkit.Mvvm** — Modern MVVM architecture implementation
- **Ollama API** — Real-time communication with local Ollama service

### Quick Start

#### Download & Run (Recommended)

Go to the [Releases](https://github.com/vipx/Maxolla/releases) page, download the latest release, extract the archive, and double-click `MaxollaOllamaManager.exe`. No installation or environment setup required.

> **Requirements:** Windows 10/11, 64-bit. Ollama must be installed and running ([ollama.com](https://ollama.com)).

#### Build from Source

```bash
# Clone the repository
git clone https://github.com/vipx/Maxolla.git

# Restore dependencies and build
dotnet restore
dotnet build --configuration Release

# Publish as single-file EXE
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# Run
./publish/v1.1.6/MaxollaOllamaManager.exe
```

Forks and free sharing are welcome. Derivative works based on this project must comply with the license terms: **attribution** (credit the original author and project), **free sharing** (no paywalls or subscription barriers), **non-commercial use only**, and derivative works must be released under a compatible license. Academic research, personal learning, and internal use by non-profit organizations are not restricted by the commercial clause. See [LICENSE](LICENSE) for details.

---

## 日本語

**Maxolla Ollama Manager** は、Windows 上で動作するローカル Ollama インスタンス管理デスクトップアプリケーションです。モデル管理_INTERFACE、リアルタイム GPU 監視、インタラクティブなチャットテスト、ログ表示、完全なデータバックアップ/リストア機能を提供します。

### 主な機能

- **モデル管理** — ローカル Ollama モデルの閲覧、安装、削除、詳細とパラメータの確認
- **チャットテスト** — アプリ内で大規模言語モデルと直接チャット；Markdown レンダリング対応（見出し、リスト、コードブロック、引用、リンクなど）；コードブロック右上のワンクリックコピー；ボタンの即時フィードバック
- **GPU 監視** — GPU 利用率、メモリ使用量、温度のリアルタイム表示
- **実行中セッション** — 現在アクティブなモデルセッションの確認
- **ログビューア** — Ollama サーバーログのリアルタイム表示、キーワードフィルタリング対応
- **バックアップとリストア** — 全モデルデータの一括バックアップ、バックアップファイルからの完全リストア
- **システムトレイ** — トレイに最小化してバックグラウンド監視
- **ダークテーマ** — 長く使えるよう丁寧にデザインされたダークテーマ
- **8言語対応** — 簡体字中国語、繁体字中国語、英語、日本語、韓国語、ロシア語、スペイン語、アラビア語

### 技術スタック

- **.NET 8** + **WPF** — ネイティブ Windows デスクトップフレームワーク
- **CommunityToolkit.Mvvm** — モダンな MVVM アーキテクチャ実装
- **Ollama API** — ローカル Ollama サービスとのリアルタイム通信

### クイックスタート

#### ダウンロードと実行（推奨）

[Releases](https://github.com/vipx/Maxolla/releases) ページから最新版をダウンロードし、アーカイブを展開して `MaxollaOllamaManager.exe` をダブルクリックしてください。インストールや環境設定は不要です。

> **動作環境：** Windows 10/11、64 ビット版。Ollama がインストールされ実行中であること（[ollama.com](https://ollama.com)）。

#### ソースからのビルド

```bash
# リポジトリをクローン
git clone https://github.com/vipx/Maxolla.git

# 依存関係を復元してビルド
dotnet restore
dotnet build --configuration Release

# 単一ファイル EXE として発行
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# 実行
./publish/v1.1.6/MaxollaOllamaManager.exe
```

Fork と無料共有を歓迎します。本プロジェクトに基づく派生作品はライセンス条項に従う必要があります：**原作者とプロジェクトの出典を明記**、**無料共有**（有料の壁や購読バリアなし）、**商用利用禁止**、派生作品は互換ライセンスでの配布が必要です。研究目的、個人学習、非営利組織内での使用は商用制限の対象外です。詳細は [LICENSE](LICENSE) をご覧ください。

---

## 한국어

**Maxolla Ollama Manager**는 Windows에서 실행되는 로컬 Ollama 인스턴스 관리 데스크톱 애플리케이션입니다. 모델 관리 인터페이스, 실시간 GPU 모니터링, 대화형 채팅 테스트, 로그 조회 및 완전한 데이터 백업/복원 기능을 제공합니다.

### 주요 기능

- **모델 관리** — 로컬 Ollama 모델 탐색, 설치, 삭제 및 모델 상세 정보 및 매개변수 확인
- **채팅 테스트** — 앱 내에서 대규모 언어 모델과 직접 채팅; Markdown 렌더링 지원(제목, 목록, 코드 블록, 인용, 링크 등); 코드 블록 우상단 원클릭 복사; 버튼 즉시 피드백
- **GPU 모니터링** — GPU 사용률, 메모리 사용량, 온도 실시간 표시
- **실행 중인 세션** — 현재 활성 모델 세션 확인
- **로그 뷰어** — Ollama 서버 로그 실시간 조회 및 키워드 필터링 지원
- **백업 및 복원** — 전체 모델 데이터 원클릭 백업; 백업 파일에서 완전한 복원
- **시스템 트레이** — 트레이로 최소화하여 백그라운드 모니터링
- **다크 테마** — 오랫동안 사용해도 눈이 피로하지 않도록 정성껏 디자인된 다크 테마
- **8개 언어 지원** — 간체 중국어, 번체 중국어, 영어, 일본어, 한국어, 러시아어, 스페인어, 아랍어

### 기술 스택

- **.NET 8** + **WPF** — 네이티브 Windows 데스크톱 프레임워크, 우수한 성능
- **CommunityToolkit.Mvvm** — 현대적인 MVVM 아키텍처 구현
- **Ollama API** — 로컬 Ollama 서비스와 실시간 통신

### 빠른 시작

#### 다운로드 및 실행 (권장)

[Releases](https://github.com/vipx/Maxolla/releases) 페이지에서 최신 버전을 다운로드하고 압축을 푼 후 `MaxollaOllamaManager.exe`를 더블클릭하세요. 설치나 환경 설정이 필요하지 않습니다.

> **요구사항:** Windows 10/11, 64비트. Ollama가 설치되어 실행 중이어야 합니다 ([ollama.com](https://ollama.com)).

#### 소스에서 빌드

```bash
# 리포지토리 클론
git clone https://github.com/vipx/Maxolla.git

# 의존성 복원 및 빌드
dotnet restore
dotnet build --configuration Release

# 단일 파일 EXE로 게시
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# 실행
./publish/v1.1.6/MaxollaOllamaManager.exe
```

Fork 및 무료 공유를 환영합니다. 본 프로젝트 기반의 파생작물은 라이선스 조건을 준수해야 합니다: **원작자와 원본 프로젝트 출처 표기**, **무료 공유** (유료 벽이나 구독 차단 금지), **상업적 이용 금지**, 파생작물은 호환 가능한 라이선스로 배포해야 합니다. 학술 연구, 개인 학습, 비영리 조직 내부 사용은 상업적 이용 제한 대상이 아닙니다. 자세한 내용은 [LICENSE](LICENSE)를 참조하세요.

---

## Русский

**Maxolla Ollama Manager** — это настольное приложение для Windows, предназначенное для управления локальными экземплярами Ollama. Оно предоставляет удобный интерфейс управления моделями, мониторинг GPU в реальном времени, интерактивное тестирование чата, просмотр логов и полноценное резервное копирование/восстановление данных.

### Основные функции

- **Управление моделями** — просмотр, установка и удаление локальных моделей Ollama; просмотр деталей и параметров модели
- **Тестирование чата** — общение с языковыми моделями прямо в приложении; поддержка рендеринга Markdown (заголовки, списки, блоки кода, цитаты, ссылки и т.д.); копирование кода одним кликом; мгновенная обратная связь на кнопках
- **Мониторинг GPU** — отображение использования GPU, памяти и температуры в реальном времени
- **Активные сессии** — просмотр текущих активных сессий модели
- **Просмотр логов** — просмотр логов сервера Ollama в реальном времени с фильтрацией по ключевым словам
- **Резервное копирование и восстановление** — резервное копирование всех данных моделей в один клик; полное восстановление из файла резервной копии
- **Системный трей** — сворачивание в трей для фонового мониторинга
- **Тёмная тема** — тщательно проработанная тёмная цветовая схема для комфортной работы
- **8 языков** — упрощённый китайский, традиционный китайский, английский, японский, корейский, русский, испанский, арабский

### Технологический стек

- **.NET 8** + **WPF** — нативный фреймворк для рабочего стола Windows с отличной производительностью
- **CommunityToolkit.Mvvm** — современная реализация архитектуры MVVM
- **Ollama API** — взаимодействие с локальным сервисом Ollama в реальном времени

### Быстрый старт

#### Скачать и запустить (рекомендуется)

Перейдите на страницу [Releases](https://github.com/vipx/Maxolla/releases), скачайте последнюю версию, распакуйте архив и дважды кликните на `MaxollaOllamaManager.exe`. Установка и настройка окружения не требуются.

> **Требования:** Windows 10/11, 64-бит. Ollama должен быть установлен и запущен ([ollama.com](https://ollama.com)).

#### Сборка из исходного кода

```bash
# Клонировать репозиторий
git clone https://github.com/vipx/Maxolla.git

# Восстановить зависимости и собрать
dotnet restore
dotnet build --configuration Release

# Опубликовать как один EXE-файл
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# Запустить
./publish/v1.1.6/MaxollaOllamaManager.exe
```

Добро пожаловать в форк и бесплатное распространение. Производные работы на основе этого проекта должны соблюдать условия лицензии: **указывать авторство** (оригинального автора и проект), **бесплатное распространение** (без платных стен и подписок), **запрет коммерческого использования**, производные работы должны распространяться под совместимой лицензией. На академические исследования, личное обучение и внутреннее использование в некоммерческих организациях коммерческое ограничение не распространяется. См. [LICENSE](LICENSE) для подробностей.

---

## Español

**Maxolla Ollama Manager** es una aplicación de escritorio para Windows que permite gestionar instancias locales de Ollama. Ofrece una interfaz clara de gestión de modelos, monitoreo de GPU en tiempo real, pruebas de chat interactivas, visualización de logs y funcionalidades completas de respaldo y restauración de datos.

### Funciones principales

- **Gestión de modelos** — explorar, instalar y eliminar modelos Ollama locales; ver detalles y configuraciones de parámetros
- **Prueba de chat** — conversar con modelos de lenguaje grandes directamente en la aplicación; renderizado de Markdown (encabezados, listas, bloques de código, citas, enlaces, etc.); copiar código con un clic en la esquina superior derecha; retroalimentación instantánea en los botones
- **Monitoreo de GPU** — visualización en tiempo real de utilización de GPU, uso de memoria y temperatura
- **Sesiones activas** — ver las sesiones de modelo actualmente activas
- **Visor de logs** — visualización en tiempo real de logs del servidor Ollama con filtrado por palabras clave
- **Respaldo y restauración** — respaldo con un clic de todos los datos de modelos; restauración completa desde archivo de respaldo
- **Bandeja del sistema** — minimizar a la bandeja para monitoreo en segundo plano
- **Tema oscuro** — esquema de colores oscuros cuidadosamente diseñado para uso prolongado
- **8 idiomas** — chino simplificado, chino tradicional, inglés, japonés, coreano, ruso, español, árabe

### Pila tecnológica

- **.NET 8** + **WPF** — marco de escritorio nativo para Windows con excelente rendimiento
- **CommunityToolkit.Mvvm** — implementación moderna de arquitectura MVVM
- **Ollama API** — comunicación en tiempo real con el servicio Ollama local

### Inicio rápido

#### Descargar y ejecutar (recomendado)

Vaya a la página de [Releases](https://github.com/vipx/Maxolla/releases), descargue la última versión, extraiga el archivo y haga doble clic en `MaxollaOllamaManager.exe`. No requiere instalación ni configuración de entorno.

> **Requisitos:** Windows 10/11, 64 bits. Ollama debe estar instalado y en ejecución ([ollama.com](https://ollama.com)).

#### Compilar desde el código fuente

```bash
# Clonar el repositorio
git clone https://github.com/vipx/Maxolla.git

# Restaurar dependencias y compilar
dotnet restore
dotnet build --configuration Release

# Publicar como EXE de archivo único
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# Ejecutar
./publish/v1.1.6/MaxollaOllamaManager.exe
```

Se aceptan forks y redistribución gratuita. Los trabajos derivados basados en este proyecto deben cumplir los términos de la licencia: **atribución** (crédito al autor original y al proyecto), **distribución gratuita** (sin muros de pago ni suscripciones), **uso no comercial**, y los trabajos derivados deben distribuirse bajo una licencia compatible. La investigación académica, el aprendizaje personal y el uso interno por parte de organizaciones sin fines de lucro no están restringidos por la cláusula comercial. Consulte [LICENSE](LICENSE) para más detalles.

---

## العربية

**Maxolla Ollama Manager** هو تطبيق سطح مكتب لنظام Windows لإدارة مثيلات Ollama المحلية. يوفر واجهة واضحة لإدارة النماذج، ومراقبة GPU في الوقت الفعلي، واختبار الدردشة التفاعلي، وعرض السجلات، ووظائف النسخ الاحتياطي والاستعادة الكاملة للبيانات.

### الميزات الرئيسية

- **إدارة النماذج** — تصفح وتثبيت وحذف نماذج Ollama المحلية؛ عرض تفاصيل النموذج وتكويناته
- **اختبار الدردشة** — الدردشة مع النماذج اللغوية الكبيرة مباشرة داخل التطبيق؛ دعم عرض Markdown (العناوين والقوائم وكتل التعليمات البرمجية والاقتباسات والروابط وما إلى ذلك)؛ نسخ التعليمات البرمجية بنقرة واحدة في الزاوية العلوية اليمنى؛ تغذية راجعة فورية على الأزرار
- **مراقبة GPU** — عرض استخدام GPU واستخدام الذاكرة ودرجة الحرارة في الوقت الفعلي
- **الجلسات النشطة** — عرض جلسات النموذج النشطة حاليًا
- **عارض السجلات** — عرض سجلات خادم Ollama في الوقت الفعلي مع تصفية الكلمات الرئيسية
- **النسخ الاحتياطي والاستعادة** — نسخ احتياطي بنقرة واحدة لجميع بيانات النموذج؛ استعادة كاملة من ملف النسخ الاحتياطي
- **علبة النظام** — تصغير إلى علبة النظام للمراقبة في الخلفية
- **السمة الداكنة** — مخطط ألوان داكن مصمم بعناية لراحة الاستخدام المطول
- **8 لغات** — الصينية المبسطة والصينية التقليدية والإنجليزية واليابانية والكورية والروسية والإسبانية والعربية

### الحزمة التقنية

- **.NET 8** + **WPF** — إطار سطح مكتب Windows الأصلي مع أداء ممتاز
- **CommunityToolkit.Mvvm** — تنفيذ حديث لهيكل MVVM
- **Ollama API** — اتصال في الوقت الفعلي مع خدمة Ollama المحلية

### البدء السريع

#### التنزيل والتشغيل (موصى به)

انتقل إلى صفحة [Releases](https://github.com/vipx/Maxolla/releases)، وقم بتنزيل أحدث إصدار، واستخرج الأرشيف، وانقر نقرًا مزدوجًا على `MaxollaOllamaManager.exe`. لا حاجة للتثبيت أو إعداد البيئة.

> **المتطلبات:** Windows 10/11، 64 بت. يجب تثبيت Ollama وتشغيله ([ollama.com](https://ollama.com)).

#### البناء من الكود المصدري

```bash
# استنساخ المستودع
git clone https://github.com/vipx/Maxolla.git

# استعادة التبعيات والبناء
dotnet restore
dotnet build --configuration Release

# النشر كملف EXE واحد
dotnet publish src/OllamaManager.App/OllamaManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/v1.1.6

# التشغيل
./publish/v1.1.6/MaxollaOllamaManager.exe
```

نرحب بعمل Fork والمشاركة المجانية. يجب أن تلتزم الأعمال المشتقة من هذا المشروع بشروط الترخيص: **نسب المصدر الأصلي** (الإسناد إلى المؤلف الأصلي والمشروع)، **المشاركة المجانية** (بدون جدران دفع أو حواجز اشتراك)، **حظر الاستخدام التجاري**، وأن تُوزع الأعمال المشتقة بترخيص متوافق. الاستخدام للأبحاث الأكاديمية والتعلم الشخصي والاستخدام الداخلي من قبل المنظمات غير التجارية لا يخضع لقيود الاستخدام التجاري. انظر [LICENSE](LICENSE) للتفاصيل.

---

## 迭代历史

本项目从最初版本一路迭代完善，经历以下主要版本：

| 版本 | 主要内容 |
|------|---------|
| v1.0.4 | 初始发布，基本模型管理功能 |
| v1.0.5 | 完善模型管理，增加模型详情页 |
| v1.0.6 | 增加 GPU 监控功能 |
| v1.0.7 | 增加运行会话管理 |
| v1.0.8 | 系统托盘支持，后台运行 |
| v1.1.0 | 大幅优化界面交互，重构 MVVM |
| v1.1.1 | 增加数据备份与还原 |
| v1.1.2 | 日志查看功能，支持关键字过滤 |
| v1.1.3 | 多语言框架搭建，8 种语言支持 |
| v1.1.4 | 深色主题全面优化 |
| v1.1.5 | 细节打磨，UI 一致性改进 |
| **v1.1.6** | **聊天测试 Markdown 渲染、代码块复制、操作按钮即时反馈、国际化标签完善** |

---

**觉得好用的话，欢迎给个 Star！**

本项目欢迎大家 Fork 进行二次开发，并免费分享给更多人使用。根据项目许可证条款，请遵守以下约定：

- **标注来源**：基于本项目的衍生作品，请在显著位置标注原作者与原始项目来源，例如 "Forked from Maxolla Ollama Manager (https://github.com/Maxolla/OllamaManager)"。
- **免费分享**：衍生作品必须以免费方式分发，不得设置付费墙、订阅或类似的收费屏障，源码须随分发版本一同提供。
- **非商用**：本软件及任何衍生作品不得用于商业目的（包括售卖、捆绑进收费产品、提供付费服务等）。
- **同等条款**：公开发布的衍生作品须采用与本协议兼容的许可证，不得改用更宽松（允许商用）的许可证。

学术研究、个人学习、非营利组织内部使用不受商用限制。如需商业合作，请联系作者获取书面许可。有什么问题或建议，欢迎提交 Issue。

如果觉得这个工具对你有帮助，请帮我点一颗 Star，这对我来说是很大的鼓励。谢谢！

---

*License — 署名-非商用-免费分享（Attribution-NonCommercial-Free Sharing）。详见 [LICENSE](LICENSE) 文件。*
