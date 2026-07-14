章プロローグ用の一枚絵をここに置く。

- 章1: この階層に「chapter1.png」を保存する（ChapterStory.GetPrologue の imagePath="UI/Prologue/chapter1"）。
  Inspector で Texture Type = Sprite (2D and UI) にしておくこと。
- 章Nのプロローグを追加する時は chapterN.png を置き、ChapterStory.GetPrologue / HasPrologue を拡張する。

BGM: 専用BGMを使う場合は Resources/BGM/prologue1.* など（ChapterStory.GetPrologue の bgmPaths 候補）に置く。
