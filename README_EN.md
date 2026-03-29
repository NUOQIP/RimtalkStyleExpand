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
- **Async Processing**: Non-blocking chunking and generation operations
- **Resume Support**: Resume interrupted chunking
- **Smart Sampling**: Auto-sample representative chunks for large files
- **LLM Description**: Auto-generate style descriptions
- **File Watcher**: Auto-detect style file changes

## Quick Start

1. Place `.txt` style files in the `Styles/` directory
2. Launch game, open Mod settings
3. Configure **Embedding API** (for chunking)
4. Click **Scan Styles** to scan files
5. Select a style, click **Chunk Style**
6. Done!

### Style File Recommendations

| Language | Recommended Size | Notes |
|----------|------------------|-------|
| Chinese | 5,000 - 50,000 characters | Too few = poor retrieval, too many = long processing |
| English | 3,000 - 30,000 words | Same as above |

> Larger files can be used, but chunking and embedding calculation will take longer.

## Requirements

- **RimTalk** Mod (required)
- **Embedding API** (for chunking and retrieval)
- **LLM API** (optional, for auto-generating style descriptions)

### Supported APIs

- Ollama (default: `http://localhost:11434/api/embeddings`)
- OpenAI Embedding API
- Other OpenAI-compatible APIs

Recommended models: `nomic-embed-text`, `text-embedding-3-small`

### Using RimTalk API Config

LLM API can reuse RimTalk's configuration:
- Supports all RimTalk providers (Google, OpenAI, DeepSeek, Player2, Local)
- Auto-gets API URL, Key, Model

## Scriban Variables

StyleExpand auto-registers these variables to RimTalk:

| Variable | Description |
|----------|-------------|
| `{{style_base_prompt}}` | Base style instruction prompt |
| `{{style_name}}` | Current style name |
| `{{style_prompt}}` | Style description |
| `{{style_chunks}}` | Retrieved example chunks |
| `{{style_full}}` | Complete style prompt (all above combined) |

### Example Template

```
[Style Instruction]
{{style_base_prompt}}

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
    │   └── Dialog_StylePreview.cs
    ├── VectorClient.cs
    ├── StyleRetriever.cs
    └── ...
```

## Progress

### v1.5 (Current)

- [x] Semantic chunking algorithm (2026 latest RAG approach)
  - Dynamic breakpoints based on sentence embedding similarity
  - Three strategies: Recursive, Semantic, Hybrid
  - Configurable breakpoint percentile threshold (default 95%)
  - Ensures each chunk is semantically coherent
- [x] Chunking parameter optimization
  - Added: min/max chunk length, overlap
  - Default parameters optimized for style replication
- [x] Settings UI redesign
  - Chunking strategy selection
  - Semantic chunking parameters
  - Parameter tooltips

#### Recommended Parameters (Style Replication)

| Parameter | Value | Description |
|-----------|-------|-------------|
| Strategy | Semantic | Ensures complete scene/dialogue chunks |
| Breakpoint Threshold | 95% | Higher threshold = larger chunks = more complete scenes |
| Min Chunk | 120 | Single sentence too short for style reference |
| Target Chunk | 450 | One dialogue scene ≈ 300-500 characters |
| Max Chunk | 900 | Complex scenes preserved intact |
| Overlap | 0 | Semantic chunking already ensures coherence |
| Top K | 3 | 3 complete chunks ≈ 1200 chars style material |
| Similarity Threshold | 0.55 | Filter irrelevant, ensure quality |
| Sample Target | 250 | Cover style variations throughout text |
| Large File Threshold | 15000 | ~4000 Chinese chars, start sampling |

**Core Goal: Each returned chunk is a complete, learnable style unit.**

### v1.4.1

- [x] API call optimization
  - Add retry mechanism (max 3 retries + exponential backoff)
  - Fix Ollama API compatibility (prompt field)
  - Fix embedding parsing failure on Chinese systems (CultureInfo)
- [x] Cache optimization
  - Add LRU cache eviction (max 1000 entries)
  - Prevent embedding cache memory overflow
- [x] LLM Prompt redesign
  - New prompt design: no pre-set analysis dimensions, let LLM decide
  - Segment sampling: beginning/middle/end sections, ~10% total
  - Output ~500 words style profile
- [x] Code refactoring
  - Centralize RimTalk API integration in `RimTalkAPIIntegration.cs`
  - Remove redundant UI elements
- [x] Recommended model update
  - Ollama: `nomic-embed-text`

### v1.4

- [x] UI redesign
  - Reorganized module order for better UX
  - Added module descriptions
  - Collapsible advanced settings
  - Simplified chunk button logic
  - Enlarged style prompt editor with scrollbar
  - Added Scriban variables section to advanced settings
- [x] Performance optimization
  - Use LongEventHandler for background chunking/generation
  - Optimize retrieval to use cached embeddings only
  - Add `RetrieveWithScores` method
- [x] Bug fixes
  - Fix style list position offset
  - Fix cache status not refreshing after clear
  - Fix uncheck variable not removing from template
  - Fix similarity threshold display
  - Fix RimTalk API config reuse returning wrong model name
- [x] RimTalk API integration
  - LLM API supports reusing RimTalk config
  - Auto-adapt API gateway URLs
- [x] Code cleanup
  - Remove unused code and fields
  - Remove unnecessary config options
  - Remove Example Phrases from LLM prompt

### v1.3

- [x] Architecture refactor
- [x] Standalone preview dialog

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

### v1.5 (Planned)

- [ ] More preset styles

## Build

```bash
# Build for 1.5
dotnet build -p:GameVersion=1.5

# Build for 1.6
dotnet build -p:GameVersion=1.6
```

## Documentation

- [Developer Guide](DEV_GUIDE.md) - Architecture, development plans

## Dependencies

- Harmony 2.x
- RimTalk Mod
- Newtonsoft.Json

## License

Based on RimTalk, follows the same license.

## Author

NUOQI_P