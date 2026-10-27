---
license: other
license_name: css10-public-domain
license_link: https://github.com/Kyubyong/css10
language:
- ja
- en
- zh
- es
- fr
- pt
pipeline_tag: text-to-speech
tags:
- tts
- vits
- multilingual
- japanese
- piper
- prosody
- css10
- mb-istft
base_model:
- ayousanz/piper-plus-base
---

# Piper Plus CSS10 Japanese (Multilingual 6-Language + MB-iSTFT-VITS2)

[CSS10 Japanese](https://github.com/Kyubyong/css10) データセットを使用して 6 言語マルチリンガルベースモデルからファインチューニングした多言語 TTS モデルです。

Decoder は **MB-iSTFT (Multi-Band inverse STFT) + PQMF** で、HiFi-GAN ベースの旧モデルから置き換えられています。prosody features (A1/A2/A3) に対応し、日本語・英語・中国語・スペイン語・フランス語・ポルトガル語の 6 言語で音声合成が可能です。

> ⚠️ **Breaking change (2026-05)**: このモデルは MB-iSTFT-VITS2 アーキテクチャの新世代版です。**piper-plus PR #320 以降のランタイム**で動作します。出力形状 `[B, 1, T]` は維持されているため、C++/Rust/C#/Go/WASM ランタイムは修正不要で本モデルを推論できます。

## Model Details

| 項目 | 値 |
|------|-----|
| アーキテクチャ | VITS (Decoder: **MB-iSTFT + PQMF**) |
| 言語 | 日本語 (ja), 英語 (en), 中国語 (zh), スペイン語 (es), フランス語 (fr), ポルトガル語 (pt) |
| サンプルレート | 22050 Hz |
| 品質 | medium |
| 音素タイプ | multilingual |
| 音素数 | 173 |
| prosody_dim | 16 |
| 話者数 | 1 |
| 言語数 | 6 |
| ベースモデル | [ayousanz/piper-plus-base](https://huggingface.co/ayousanz/piper-plus-base) (MB-iSTFT 版、571 話者、75 epoch スクラッチ) |
| ファインチューニング | 50 epochs, lr=2e-5, freeze-dp |
| 学習データ | CSS10 Japanese (6,841 発話) |
| ONNX エクスポート | stochastic + EMA + FP16 (38MB) |
| Decoder upsample | `(4, 4) × iSTFT(4) × PQMF(4) = 256x` |

## MB-iSTFT-VITS2 Decoder の利点

| メトリック | 旧 HiFi-GAN | **MB-iSTFT (本モデル)** | 改善 |
|-----------|-----------|----------------------|------|
| CPU ONNX p50 (100 phoneme) | 168.2 ms | **76.2 ms** | **2.21x 高速化** |
| Decoder 単体 | — | — | **~3.6x** (論文値と同等) |
| 出力形状 | `[B, 1, T]` | `[B, 1, T]` | 変化なし (ランタイム互換) |

## Usage

### C++ CLI (プリビルドバイナリ)

```bash
# モデルダウンロード
./piper --download-model css10 --model-dir models

# 日本語
echo 'こんにちは、今日は良い天気ですね。' | \
  ./piper --model models/css10-ja-6lang-fp16.onnx --output_file output_ja.wav

# 英語
echo 'Hello, how are you today?' | \
  ./piper --model models/css10-ja-6lang-fp16.onnx --language en --output_file output_en.wav

# 中国語
echo '你好，今天天气很好。' | \
  ./piper --model models/css10-ja-6lang-fp16.onnx --language zh --output_file output_zh.wav

# スペイン語
echo '¿Hola, cómo estás hoy?' | \
  ./piper --model models/css10-ja-6lang-fp16.onnx --language es --output_file output_es.wav

# フランス語
echo 'Bonjour, comment allez-vous?' | \
  ./piper --model models/css10-ja-6lang-fp16.onnx --language fr --output_file output_fr.wav

# ポルトガル語
echo 'Olá, como você está hoje?' | \
  ./piper --model models/css10-ja-6lang-fp16.onnx --language pt --output_file output_pt.wav
```

### Python 推論

```bash
# インストール
pip install piper-tts-plus

# 日本語
uv run python -m piper_train.infer_onnx \
  --model css10-ja-6lang-fp16.onnx \
  --config config.json \
  --output-dir ./output \
  --text "こんにちは、今日は良い天気ですね。" \
  --language ja-en-zh-es-fr-pt \
  --noise-scale 0.667

# 英語
uv run python -m piper_train.infer_onnx \
  --model css10-ja-6lang-fp16.onnx \
  --config config.json \
  --output-dir ./output \
  --text "Hello, how are you today?" \
  --language ja-en-zh-es-fr-pt \
  --noise-scale 0.667
```

### 推奨推論パラメータ

| パラメータ | 値 | 説明 |
|-----------|-----|------|
| `--noise-scale` | 0.667 | デフォルト推奨値 |
| `--length-scale` | 1.0–1.5 | 1.0 だと早口傾向。自然な発話速度には 1.2–1.5 推奨 |
| `--speaker-id` | 0 | 単一話者 |
| `--language` | ja-en-zh-es-fr-pt | 6 言語自動検出 |

## Training

### ベースモデル

[ayousanz/piper-plus-base](https://huggingface.co/ayousanz/piper-plus-base) の MB-iSTFT-VITS2 ベースモデル (571 話者、508,187 発話、75 epoch スクラッチ学習) からファインチューニング。

### 転移学習方式

1. `--resume-from-multispeaker-checkpoint` でベースモデルからロード
   - `emb_g` (571 話者分の話者埋め込み) を自動除去
   - `emb_lang` に conditioning 分布補正を適用
   - `--freeze-dp` を自動有効化 (Duration Predictor の catastrophic forgetting 防止)
2. 50 epoch ファインチューニング (6,841 発話、lr=2e-5)
3. ONNX エクスポート (EMA + stochastic + FP16)

### 学習コマンド

```bash
WANDB_MODE=disabled \
uv run python -m piper_train \
  --dataset-dir /path/to/css10-ja-6lang \
  --prosody-dim 16 \
  --accelerator gpu --devices 1 --precision 32-true \
  --max_epochs 50 --batch-size 16 --samples-per-speaker 4 \
  --checkpoint-epochs 5 --quality medium \
  --base_lr 2e-5 --disable_auto_lr_scaling \
  --ema-decay 0.9995 \
  --max-phoneme-ids 400 \
  --no-wavlm \
  --val-every-n-epochs 5 \
  --resume-from-multispeaker-checkpoint /path/to/base_model.ckpt \
  --default_root_dir /path/to/output
```

## Files

- `css10-ja-6lang-fp16.onnx` - ONNX モデル (38MB, FP16, MB-iSTFT-VITS2)
- `config.json` - モデル設定 (173 音素マップ、6 言語)

## Credits

- **CSS10 Dataset**: [Kyubyong/css10](https://github.com/Kyubyong/css10)
- **Piper TTS**: [rhasspy/piper](https://github.com/rhasspy/piper)
- **MB-iSTFT-VITS**: [Kawamura et al., 2023](https://arxiv.org/abs/2210.15975)
- **Piper Plus**: [ayutaz/piper-plus](https://github.com/ayutaz/piper-plus) (PR #320 で MB-iSTFT 統一)

## License

CSS10 データセットのライセンスに準じます。詳細は [CSS10 GitHub](https://github.com/Kyubyong/css10) を参照してください。
