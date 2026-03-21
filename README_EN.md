# RimTalk StyleExpand

English | [中文](README.md)

**Make RimTalk dialogues imitate specified writing styles**

A style expansion Mod based on vector retrieval, supporting custom styles.

## Features

- **Style Management**: One txt file = one style
- **Vector Retrieval**: Automatically retrieve similar style fragments based on character traits
- **Auto Variable Registration**: `{{style_xxx}}` variables auto-registered to RimTalk
- **Caching**: Chunking results and embeddings cached locally
- **Localization**: Chinese / English
- **Multi-version**: RimWorld 1.5 / 1.6
- **Async API**: Non-blocking vector retrieval
- **Batch Processing**: Chunk all styles with one click
- **Resume Support**: Resume interrupted chunking
- **Smart Sampling**: Auto-sample representative chunks for large files
- **LLM Description**: Auto-generate style descriptions

## Quick Start

1. Place `.txt` style files in the `Styles/` directory
2. Launch game, open Mod settings
3. Click **Scan Styles** to scan files
4. Select a style
5. Click **Chunk Style** to process
6. Done!

### Style File Recommendations

| Language | Recommended Size | Notes |
|----------|------------------|-------|
| Chinese | 5,000 - 50,000 characters | Too few = poor retrieval, too many = long processing |
| English | 3,000 - 30,000 words | Same as above |

> Larger files can be used, but chunking and embedding calculation will take longer.

## Requirements

- **RimTalk** Mod (required)
- **Vector API** (local Ollama or cloud API)

### Supported APIs

- Ollama (default: `http://localhost:11434/api/embeddings`)
- OpenAI Embedding API
- Other OpenAI-compatible APIs

Recommended models: `bge-small-zh`, `text-embedding-3-small`

## Scriban Variables

StyleExpand auto-registers these variables to RimTalk:

| Variable | Description |
|----------|-------------|
| `{{style_name}}` | Current style name |
| `{{style_prompt}}` | Style description |
| `{{style_chunks}}` | Retrieved example chunks |
| `{{style_full}}` | Complete style prompt (all above combined) |

### Example Template

```
[Style Instruction]
Please imitate the following writing style ({{style_name}}) when generating dialogue:

{{style_prompt}}

{{style_chunks}}
```

## File Structure

```
StyleExpand/
├── About/About.xml
├── Languages/
│   ├── English/Keyed/StyleExpand.xml
│   └── ChineseSimplified/Keyed/StyleExpand.xml
├── 1.5/Assemblies/
│   └── RimTalk-StyleExpand.dll
├── 1.6/Assemblies/
│   └── RimTalk-StyleExpand.dll
├── Styles/
│   ├── Tsundere.txt
│   ├── Classical.txt
│   └── .cache/           # Auto-generated cache
└── Source/
    ├── API/
    │   ├── RimTalkAPIIntegration.cs
    │   └── StyleVariableProvider.cs
    ├── UI/
    │   ├── SettingsWindow.cs
    │   ├── SettingsUIDrawers.cs
    │   ├── Dialog_StylePreview.cs
    │   └── HelpWindow.cs
    ├── VectorClient.cs
    ├── StyleRetriever.cs
    └── ...
```

## Progress

### v1.3 (Current)

- [x] Architecture refactor (based on ExpandMemory patterns)
  - Auto API variable registration
  - VectorClient singleton pattern
  - Async vector retrieval
  - Content hash caching
  - Modular code structure (API/, UI/ subdirectories)
- [x] Standalone preview dialog
  - Variable selector
  - Query template editor
  - Retrieval result display

### v1.2

- [x] Style preview
- [x] Chunking algorithm optimization

### v1.1

- [x] Batch chunking + resume
- [x] Large file sampling
- [x] LLM auto-generate style prompt
- [x] File change auto-reload

### v1.0

- [x] Vector retrieval
- [x] Prompt injection
- [x] Style management
- [x] Configuration UI
- [x] Localization
- [x] RimWorld 1.5 / 1.6 support

### v1.4 (Planned)

- [ ] More preset styles
- [ ] Performance optimization

## Build

```bash
# Build for 1.5
dotnet build -p:GameVersion=1.5

# Build for 1.6
dotnet build -p:GameVersion=1.6
```

## Documentation

- [Developer Guide](DEV_GUIDE.md) - Architecture, development plans
- [Test Cases](TEST_CASES.md) - Detailed test checklist

## Dependencies

- Harmony 2.x
- RimTalk Mod
- Newtonsoft.Json

## License

Based on RimTalk, follows the same license.

## Author

NUOQI_P