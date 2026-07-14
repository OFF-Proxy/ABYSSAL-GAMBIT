using System.Collections.Generic;
using UnityEngine;

// STORY v2: 章中心の物語データ（中ボス別キャラ化＝色ティント＋表示名＋かませ台本）。
// 同じ素体ユニットが章内/章跨ぎで再登場しても、登場枠(slot)ごとに別個体として扱う（スライム/スライムベス方式）。
// slot は「その章で何体目の中ボス戦か」（1始まり、プレイ順）。命名・台詞=Codex、色ティント=Cowork。
// 章導入VN・節目チャッターは Stage2 で追加予定。
public static class ChapterStory
{
    public struct MidVariant
    {
        public string name;     // 表示名（異名）。ダイアログ名前枠で素体名の代わりに使う。
        public Color tint;      // スプライト色ティント（乗算）。見た目だけ。ステータスは不変。
        public string[] lines;  // かませ台本（偶数index=ボス/奇数index=主人公、交互）。主人公共通。
    }

    // 章 → (中ボス素体ユニットID → 別キャラ化バリアント配列)。
    // ルート選択で実際に出る素体に合わせて名前/台本/色を出すため、登場順(slot)ではなく素体IDで引く。
    // 同じ素体が1ラン内で複数回出る場合（固定戦＋ノード等）は occurrence で別キャラ（傷鬣ロウガ等）へ切替。
    private static Dictionary<int, Dictionary<string, MidVariant[]>> midByChapter;

    // 章chapterで素体unitIdが occurrence 回目（0始まり）の中ボスとして出た時のバリアント。
    // 配列が複数あれば occurrence で巡回（同素体2回目は別キャラ）。無ければ null（→汎用フォールバック）。
    public static MidVariant? GetMidVariantForUnit(int chapter, string unitId, int occurrence = 0)
    {
        if (midByChapter == null) Build();
        if (string.IsNullOrEmpty(unitId)) return null;
        string key = unitId.ToLowerInvariant();
        if (midByChapter.TryGetValue(chapter, out var map) && map != null
            && map.TryGetValue(key, out MidVariant[] arr) && arr != null && arr.Length > 0)
        {
            int i = ((occurrence % arr.Length) + arr.Length) % arr.Length;
            return arr[i];
        }
        return null;
    }

    private static MidVariant V(string name, Color tint, string l1, string l2, string l3)
        => new MidVariant { name = name, tint = tint, lines = new[] { l1, l2, l3 } };

    // === 章プロローグ（章開始時・盤面の前の全画面一枚絵＋ナレーション字幕＋専用BGM）。起承転結の「起」。===
    public struct Prologue { public string imagePath; public string[] bgmPaths; public string[] lines; }

    // 章にプロローグがあるか（あれば盤面前に全画面プロローグを出し、立ち絵VN(CHOPEN)はスキップ）。
    public static bool HasPrologue(int chapter) => chapter == 1;

    public static Prologue? GetPrologue(int chapter)
    {
        if (chapter == 1)
        {
            return new Prologue
            {
                imagePath = "UI/Prologue/Chapter1_Prologue", // Assets/Resources/UI/Prologue/Chapter1_Prologue.png
                bgmPaths = new[] { "music/music_prologue" }, // Assets/Resources/music/music_prologue.ogg
                // 本文はCodex確定稿に差し替え予定（暫定＝起承転結の「起」を最低限提示）。
                lines = new[]
                {
                    "終幕の眠りは、音も刃も立てずに世界の端から広がり、人々の朝を静かに奪い始めていた。",
                    "人は死なない。ただ痛みも願いも選ぶ力も抜かれ、穏やかな顔で戻らぬ眠りへ落ちていく。",
                    "各地に二十の門が現れ、強者と災厄が門守りとなって、目覚めようとする者の道を塞いだ。",
                    "門の奥には、終焉を慈愛のように観測する者、アルカナが待つという噂だけが静かに残った。",
                    "眠りを解く術を求めるなら、門を越え、観測片を集め、その名のもとへ辿り着くしかない。",
                    "最初の白門跡には、炎と千切れた鎖の中で、翠の片眼を燃やす英雄キャリバーが立っていた。",
                },
            };
        }
        return null;
    }

    // === 節目チャッター（ラウンド進行で3フェーズ。章ボスが遠くから煽る声など）。===
    public enum ChatterPhase { Early = 0, Mid = 1, PreBoss = 2 }
    private static Dictionary<int, Dictionary<ChatterPhase, string[]>> chatterByChapter; // [chapter][phase] = {speaker, line}

    // 章chapterのフェーズpのチャッター。あれば true（speaker/lineを返す）。
    public static bool TryGetChatter(int chapter, ChatterPhase p, out string speaker, out string line)
    {
        if (chatterByChapter == null) BuildChatter();
        speaker = null; line = null;
        if (chatterByChapter.TryGetValue(chapter, out var byPhase) && byPhase.TryGetValue(p, out string[] sp) && sp.Length >= 2)
        { speaker = sp[0]; line = sp[1]; return true; }
        return false;
    }

    private static void BuildChatter()
    {
        chatterByChapter = new Dictionary<int, Dictionary<ChatterPhase, string[]>>();
        chatterByChapter[1] = new Dictionary<ChatterPhase, string[]>
        {
            { ChatterPhase.Early,   new[]{ "キャリバー（遠く）", "白門を閉じろ……誰も通すな。もう泣き声は聞きたくない、守れぬ命ならせめて静かに眠らせてやる。" } },
            { ChatterPhase.Mid,     new[]{ "キャリバー（遠く）", "従え、まだ剣を握れる者たちよ。近づく足音は希望ではない、また誰かを奪う影だと覚えておけ。" } },
            { ChatterPhase.PreBoss, new[]{ "キャリバー（遠く）", "聞こえる……優しい声が、もう戦わなくていいと告げている。だが私は守護者だ、終わらせることでしか守れぬなら、そうする。" } },
        };
        chatterByChapter[2] = new Dictionary<ChatterPhase, string[]>
        {
            { ChatterPhase.Early,   new[]{ "ルーク（遠く）", "進むな。足跡は傷を増やす。石の前で止まれ。" } },
            { ChatterPhase.Mid,     new[]{ "ルーク（遠く）", "止まれば失わぬ。眠れば選ばぬ。門はそのためにある。" } },
            { ChatterPhase.PreBoss, new[]{ "ルーク（遠く）", "ここが終点だ。なお進むなら、痛みごと石に刻め。" } },
        };
    }

    private static void Build()
    {
        midByChapter = new Dictionary<int, Dictionary<string, MidVariant[]>>();

        // === 第1章（Caliber/後悔・英雄暴走版）。中ボス＝白門の門守りたち。===
        // 出典: CLAUDE_READY_chapter1_rewrite_ver1.0。ルート選択で出る素体(neutral_*)に合わせて名前/台本/色を割当。
        // 中ボスプールの素体は neutral_beastmaster / gnasher / rawr / rok / zukong の5種。
        // beastmaster と gnasher は固定戦＋ノードで1ラン2回出るため、2回目は別個体（ロウガ/メイリン）へ。
        // 同素体の2体目は専用リカラー素体（別ユニットID）として出すため、各IDに1キャラずつ割当。
        // リカラー素体は画像自体が着色済みなので tint は白（二重着色しない）。
        midByChapter[1] = new Dictionary<string, MidVariant[]>
        {
            { "neutral_beastmaster", new[]{ V("金鬣の獣戦士バルガ", new Color(1.00f, 0.82f, 0.55f),
                "ガハハッ、白門へ来る足音にしちゃあ軽いな。俺は金鬣のバルガ、キャリバー卿の咆哮に惚れて牙を預けた獣戦士だ。弱い奴を眠らせる慈悲、悪くねえ響きだろう。",
                "力のある者が勝手に慈悲を決めれば、弱い者は声を上げる前に終わらされる。吠えるなら、守るために吠えろ。眠らせるための牙はここで止める。",
                "いい目だ、噛み応えがありそうじゃねえか。だがキャリバー卿の黄金の巨鎧に届く前に、俺の爪で覚悟を裂いてやる。獣の道は、血の匂いに嘘をつかねえ。") } },
            { "neutral_beastmaster_crimson", new[]{ V("傷鬣の獣戦士ロウガ", Color.white,
                "若い牙はよく吠えるが、俺は吠え飽きた。傷鬣のロウガだ。白門で守れなかった匂いを嗅いだ者は知っている、キャリバー卿の眠りは臆病ではなく、疲れ切った優しさだ。",
                "疲れ切った優しさなら、なおさら休ませるべきはキャリバー自身だ。周りの命を眠らせてまで背負わせるのは、忠義じゃない。ただ痛みに寄りかかっているだけだ。",
                "痛いところを噛むな、若造。だがその言葉、嫌いじゃねえ。だから手加減はしない。俺の古い爪を越えられるなら、卿の疲れた背中に触れる資格を認めてやる。") } },
            { "neutral_gnasher", new[]{ V("封符の屍爪リンシェン", new Color(0.78f, 0.66f, 0.92f),
                "ちりん、ちりん……札が鳴る。キャリバー様の命令、よく染みている。眠れば痛くない、迷わない、泣かない。だからリンシェンの爪は、優しく深く、心まで止める。",
                "痛くないから救いだと決めつけるな。泣くことも迷うことも、生きて選ぶ者のものだ。札に縛られた命令で、それを奪わせはしない。",
                "生きて選ぶ……重い言葉、腐った胸にも少し響く。でも札は剥がれない。キャリバー様の白い慈悲が、リンシェンを前へ押す。ならば爪で、あなたの選択を試す。") } },
            { "neutral_gnasher_ice", new[]{ V("白符の屍爪メイリン", Color.white,
                "しずか、しずか……白い札がそう歌うの。メイリンは怖い夢を見たくない。キャリバー様が眠れば怖くないと言ったから、爪を立てるの。みんな眠れば、泣かないでしょう。",
                "怖い夢から逃げたい気持ちは分かる。でも、全部を眠らせても朝は来ない。泣く人がいるなら、そばに立って起こす。怖いままでも進む道はある。",
                "朝……メイリン、もう長く見ていない。けれど、その言葉は少しあたたかい。だから壊してみて。札より強い朝を持っているなら、この冷たい爪を越えて見せて。") } },
            { "neutral_rawr", new[]{ V("鋼牙の機豹ラウル", new Color(0.74f, 0.86f, 0.92f),
                "捕捉完了、侵入者の心拍上昇を確認。鋼牙の機豹ラウル、キャリバー卿の防衛命令を実行する。恐怖、迷い、希望、すべて戦場を乱すノイズとして処理する。",
                "心拍を測れても、そこにある理由までは測れない。恐れても進む者がいる。迷いながら守る者がいる。ノイズと呼んで切り捨てるなら、こちらも止まれない。",
                "反論を記録、危険度を上方修正。面白い、ただの侵入者ではないらしい。ならば装甲の牙で、その理由ごと噛み砕く。白門に不要な未来は通さない。") } },
            { "neutral_rok", new[]{ V("白門の岩顎ゴルム", new Color(0.82f, 0.74f, 0.62f),
                "ゴルム、門を守る。白門、砕けた。人、泣いた。キャリバー、立っていた。だからゴルムも立つ。小さい声も、大きい願いも、門を揺らすものは全部止める。",
                "立ち続ける強さは分かる。けれど、守るために立つなら、通すべき声まで潰してはいけない。キャリバーを止める声も、白門を守るために必要だ。",
                "難しい言葉、石の頭には重い。だが胸の奥、少し揺れた。揺れたからこそ、確かめる。ゴルムの岩顎を越えろ。砕けぬ願いなら、石でも覚える。") } },
            { "neutral_zukong", new[]{ V("雲棍の猿将ズーコン", new Color(0.74f, 0.92f, 0.70f),
                "おうおう、ずいぶん真面目な顔で来たな。雲棍のズーコン様が相手をしてやる。キャリバー卿は重すぎる後悔を背負っておられる、だから軽い足で近づく奴は俺が叩き落とす。",
                "軽く見える足でも、背負っているものはある。笑っている者にも、震えている者にも、進む理由がある。道化のように跳ねるなら、せめて人の痛みを踏むな。",
                "ははっ、いい返しだ。痛みを踏むなと来たか。だが俺は猿将、踏み台も崖も笑って越える。お前の理由が本物なら、この棍をくぐってキャリバー卿まで届かせてみろ。") } },
        };

        // === 第2章（neutral_rook/停滞・門番）。中ボス＝「進ませない」門守りたち。===
        // 固定4枠は rok系4個体（基本＋色違いリカラー素体）で「石の門守り」を統一。ノードは非rok 3種で変化を出す。
        // リカラー素体（steelblue/gold/mossgreen）は画像が着色済みなので tint 白。
        midByChapter[2] = new Dictionary<string, MidVariant[]>
        {
            { "neutral_rok", new[]{ V("灰道の足止めゴーレム", new Color(0.83f, 0.80f, 0.73f),
                "灰道は進む者を重くする。諦めた足だけが、ここで静かに眠れる。",
                "重くても足は出せる。静かに眠るためではなく、眠る人を起こすために進む。",
                "では沈め。灰の下で、進む理由がまだ残るか量ってやる。") } },
            { "neutral_rok_steelblue", new[]{ V("停足の番人オルム", Color.white,
                "止まることも守りだ。動かなければ、誰かを失う場所へ辿り着かずに済む。",
                "止まって守れるものもある。でも、止まったままでは救えない命もある。",
                "その言葉を門へ刻め。歩く痛みを知らぬ者に、この先の石は開かぬ。") } },
            { "neutral_rok_gold", new[]{ V("第二門の番牙ロク", Color.white,
                "ルーク様は動かぬ。だから強い。揺れる心で、あの門を越えられると思うな。",
                "揺れても進める。迷いがあるからこそ、止まっていい道と進む道を選べる。",
                "選ぶ足は脆い。番牙が最後に試す、揺れる心で石を越えられるか。") } },
            { "neutral_rok_mossgreen", new[]{ V("門石のガロ", Color.white,
                "第二門の前で足を止めろ。ルーク様の石影より先へ進む者は、傷だけを増やす。",
                "傷が増えても、届くべき場所がある。門前で立ち尽くすために来たわけじゃない。",
                "ならば石に膝を打て。進む足がどれほど脆いか、門石が教えてやる。") } },
            { "neutral_gnasher", new[]{ V("眠り坂の門荒らしゼド", new Color(0.80f, 0.73f, 0.87f),
                "この坂で眠った者は幸せそうだった。お前も門を越えず、静かな顔で横になれ。",
                "幸せそうに見えても、選ぶ声が消えている。私はその声を取り戻しに来た。",
                "声など痛みを呼ぶだけだ。眠り坂の下で、余計な願いごと黙らせてやる。") } },
            { "neutral_beastmaster", new[]{ V("閉門の獣バルド", new Color(0.89f, 0.71f, 0.61f),
                "グルル……門は閉じた。戻れ。前へ行くほど、血と眠りの匂いが濃くなる。",
                "匂いが濃いなら、なおさら放っておけない。道が閉じたなら開けるだけだ。",
                "なら牙で止める。閉じた門に逆らう足は、噛み砕かれて当然だ。") } },
            { "neutral_zukong", new[]{ V("石鎖の監視者ミル", new Color(0.73f, 0.82f, 0.85f),
                "石鎖は優しい。動きたい心を縛れば、もう転ばず、誰も置いていかない。",
                "転ばないために縛られるなら、立ち上がる力まで失ってしまう。",
                "立ち上がる力がまた傷を呼ぶ。ならば鎖で、その未来ごと固定する。") } },
        };
    }
}
