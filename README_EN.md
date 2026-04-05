# RimTalk StyleExpand

English | [中文](README.md)

**RimTalk Style Extension Module — Apply any writing style to dialogue generation**

A style customization Mod based on vector retrieval. Users provide style materials, the Mod analyzes them and injects style features into RimTalk's dialogue generation.

## Core Features

- **Style Management** — One `.txt` file = one style, fully user-defined
- **Semantic Chunking** — Dynamic chunking based on embedding similarity, ensuring semantic coherence
- **Vector Retrieval** — Auto-retrieve relevant style chunks based on character traits
- **LLM Analysis** — Optional auto-generation of style descriptions from materials
- **RimTalk API Integration** — Auto-register Scriban variables, no manual configuration

## Architecture

```
Style Material (.txt)
       ↓
  Semantic Chunker ← Embedding API
       ↓
  Vector Index Cache
       ↓
  Retrieval Engine ← Character query
       ↓
  Prompt Injection → RimTalk System Prompt
```

### RimTalk Variable Registration

| Variable | Description |
|----------|-------------|
| `{{style_name}}` | Current style name |
| `{{style_prompt}}` | LLM-generated style description |
| `{{style_chunks}}` | Retrieved example chunks |
| `{{style_full}}` | Combined complete prompt |

### PromptEntry Injection

Auto-creates "Style Context" entry in RimTalk preset, positioned after System Prompt:

```
System Prompt (RimTalk)
      ↓
Style Context (StyleExpand) ← {{style_full}}
      ↓
Memory Context (ExpandMemory)
      ↓
User Message
```

## Quick Start

```
1. Place style materials in Styles/ directory (.txt files)
2. Launch game → Mod Settings → StyleExpand
3. Configure Embedding API (for chunking and retrieval)
4. Click "Scan Styles" → Select style → "Chunk Style"
5. Done — RimTalk dialogues will now reflect the style
6. Recommended: Configure LLM API and click "Generate Description" for better results
```

> 💡 **Recommended: Use "Generate Style Description" feature**: The MOD includes LLM analysis to automatically analyze style samples and generate style descriptions, producing better results than using retrieved chunks alone.

### Style Material Recommendations

| Metric | Recommended | Notes |
|--------|-------------|-------|
| Chinese | 5,000 - 20,000 chars | Too few = poor retrieval, too many = long processing |
| English | 3,000 - 15,000 words | Same as above |

> Larger files work, but chunking and embedding computation takes longer.

## Requirements

### Required

- **RimTalk** Mod
- **Embedding API** (for chunking and retrieval)

### Optional

- **LLM API** (for auto style description, can reuse RimTalk config)

### Supported APIs

- Ollama (default)
- OpenAI Embedding API
- Other OpenAI-compatible APIs

Recommended models: `nomic-embed-text`, `mxbai-embed-large`, `text-embedding-3-small`

## Chunking Parameters

Core parameters for semantic chunking:

| Parameter | Default | Description |
|-----------|---------|-------------|
| Breakpoint Threshold | 80% | Higher = longer chunks |
| Min Chunk Length | 100 | Minimum chunk size |
| Target Chunk Length | 400 | Target chunk size |
| Max Chunk Length | 800 | Maximum chunk size |

## Project Structure

```
StyleExpand/
├── About/About.xml
├── Languages/
│   ├── English/Keyed/
│   └── ChineseSimplified/Keyed/
├── 1.5/Assemblies/
├── 1.6/Assemblies/
├── Styles/                  # User style materials
│   └── .cache/              # Auto-generated cache
└── Source/
    ├── API/
    │   ├── RimTalkAPIIntegration.cs
    │   └── StyleVariableProvider.cs
    ├── SemanticChunker.cs
    ├── StyleRetriever.cs
    ├── VectorClient.cs
    ├── LLMClient.cs
    ├── EmbeddingCache.cs
    ├── PromptBuilder.cs
    └── UI/
        ├── SettingsWindow.cs
        └── Dialog_StylePreview.cs
```

## Build

```bash
dotnet build -p:GameVersion=1.5 -c Release
dotnet build -p:GameVersion=1.6 -c Release
```

## Dependencies

- Harmony 2.x
- RimTalk Mod
- Newtonsoft.Json

## Version History

### v1.0.3

- Fixed LLM API authentication issue (Google/cloud providers now work correctly)
- Improved resizable text area interaction
- Improved help window formatting

### v1.0.2

- Fixed Chinese encoding issues
- Fixed Scriban variable registration issues
- Optimized default prompt templates

### v1.0.1

- Added "Get Models" feature to fetch available models from Ollama/OpenAI
- Fixed Ollama API compatibility (new /api/embed endpoint)
- Fixed config file sharing violation issue
- Improved new player experience: API status detection, auto-select chunked style
- Updated help documentation with Ollama startup instructions

### v1.0

- Semantic chunking algorithm
- Vector retrieval injection
- RimTalk API integration
- LLM style analysis
- Complete settings UI
- English/Chinese localization
- RimWorld 1.5 / 1.6 support

## License

Based on RimTalk, follows the same license.

## Author

NUOQI_P

## Feedback

GitHub: https://github.com/NUOQIP/RimtalkStyleExpand