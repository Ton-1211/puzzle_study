using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    const int TRANS_TIME = 3; // 移動速度遷移時間
    const int ROT_TIME = 3; // 回転遷移時間
    // 落下制御
    const int FALL_COUNT_UNIT = 120; // ひとマス落下するカウント数
    const int FALL_COUNT_SPD = 10; // 落下速度
    const int FALL_COUNT_FAST_SPD = 20; // 高速落下時の速度
    const int GROUND_FRAMES = 50; // 設置移動可能時間
    // 落下制御
    int _fallCount = 0;
    int _groundFrame = GROUND_FRAMES;// 設置時間

    // 得点
    uint _addictiveScore = 0;

    enum RotState
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,

        Invalid = -1,
    }

    [SerializeField] PuyoController[] _puyoControllers = new PuyoController[2] { default!, default! };
    [SerializeField] BoardController boardController = default!;

    Vector2Int _position;// 軸ぷよの位置
    RotState _rotate = RotState.Up;// 角度は 0:上 1:右　2:下 3:左　でもつ（子ぷよの位置）

    AnimationController _animationController = new AnimationController();
    Vector2Int _last_position;
    RotState _last_rotate = RotState.Up;

    LogicalInput _logicalInput = new();
    // Start is called before the first frame update
    void Start()
    {
        gameObject.SetActive(false);// ぷよの種類が設定されるまで眠る
    }

    static readonly Vector2Int[] rotate_tbl = new Vector2Int[]
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };
    public void SetLogicalInput(LogicalInput reference)
    {
        _logicalInput = reference;
    }
    private static Vector2Int CalcChildPuyoPos(Vector2Int pos, RotState rot)
    {
        return pos + rotate_tbl[(int)rot];
    }
    // 新しくぷよを出す
    public bool Spawn(PuyoType axis, PuyoType child)
    {
        // 初期位置に出せるか確認
        Vector2Int position = new(2, 12);// 初期位置
        RotState rotate = RotState.Up;// 最初は上向き
        if (!CanMove(position, rotate)) return false;

        // パラメータの初期化
        _position = _last_position = position;
        _rotate = _last_rotate = rotate;
        _animationController.Set(1);
        _fallCount = 0;
        _groundFrame = GROUND_FRAMES;

        // ぷよを出す
        _puyoControllers[0].SetPuyoType(axis);
        _puyoControllers[1].SetPuyoType(child);

        _puyoControllers[0].SetPos(new Vector3((float)_position.x, (float)_position.y, 0.0f));
        Vector2Int posChild = CalcChildPuyoPos(_position, _rotate);
        _puyoControllers[1].SetPos(new Vector3((float)posChild.x, (float)posChild.y, 0.0f));

        gameObject.SetActive(true);

        return true;
    }
    private bool CanMove(Vector2Int pos, RotState rot)
    {
        if (!boardController.CanSettle(pos)) return false;
        if (!boardController.CanSettle(CalcChildPuyoPos(pos, rot))) return false;

        return true;
    }

    void SetTransition(Vector2Int pos, RotState rot, int time)
    {
        // 隙間のために保存しておく
        _last_position = _position;
        _last_rotate = _rotate;

        // 値の更新
        _position = pos;
        _rotate = rot;

        _animationController.Set(time);
    }

    private bool Translate(bool is_right)
    {
        // 仮想的に移動できるか検証する
        Vector2Int pos = _position + (is_right ? Vector2Int.right : Vector2Int.left);
        if (!CanMove(pos, _rotate)) return false;

        // 実際に移動
        SetTransition(pos, _rotate, TRANS_TIME);

        return true;
    }

    bool Rotate(bool is_right)
    {
        RotState rot = (RotState)(((int)_rotate + (is_right ? +1 : +3)) & 3);

        Vector2Int pos = _position;
        switch (rot)
        {
            case RotState.Down:
                // 右(左)から下：自分の下か右(左)下にブロックがあれば引きあがる
                if (!boardController.CanSettle(pos + Vector2Int.down) ||
                    !boardController.CanSettle(pos + new Vector2Int(is_right ? 1 : -1, -1)))
                {
                    pos += Vector2Int.up;
                }
                break;

            case RotState.Right:
                // 右：右が埋まっていれば、左に移動
                if (!boardController.CanSettle(pos + Vector2Int.right)) pos += Vector2Int.left;
                break;

            case RotState.Left:
                // 左：左が埋まっていれば、右に移動
                if (!boardController.CanSettle(pos + Vector2Int.left)) pos += Vector2Int.right;
                break;

            case RotState.Up:
                break;

            default:
                Debug.Assert(false);
                break;
        }
        if (!CanMove(pos, rot)) return false;

        // 実際に移動
        SetTransition(pos, rot, ROT_TIME);

        return true;
    }

    void Settle()
    {
        // 直接着地
        bool is_set0 = boardController.Settle(_position,
             (int)_puyoControllers[0].GetPuyoType());
        Debug.Assert(is_set0);

        bool is_set1 = boardController.Settle(CalcChildPuyoPos(_position, _rotate),
             (int)_puyoControllers[1].GetPuyoType());
        Debug.Assert(is_set1);

        gameObject.SetActive(false);
    }

    void QuickDrop()
    {
        // 落ちれる一番下まで落ちる
        Vector2Int pos = _position;
        do
        {
            pos += Vector2Int.down;
        } while (CanMove(pos, _rotate));
        pos -= Vector2Int.down;// 一つ上の場所（最後に置けた場所）に戻す

        _position = pos;

        Settle();
    }

    bool Fall(bool is_fast)
    {
        _fallCount -= is_fast ? FALL_COUNT_FAST_SPD : FALL_COUNT_SPD;

        // ブロックを飛び越えたら、行けるのかチェック
        while(_fallCount < 0)// ブロックが飛ぶ可能性を考え複数落下に対応
        {
            if(!CanMove(_position + Vector2Int.down, _rotate))
            {
                // 落ちれないなら
                _fallCount = 0; // 動きを止める
                if (0 < --_groundFrame) return true;// 時間があるなら、移動・回転可能

                // 時間切れになったら本当に固定
                Settle();
                return false;
            }

            // 落ちれるなら下に進む
            _position += Vector2Int.down;
            _last_position += Vector2Int.down;
            _fallCount += FALL_COUNT_UNIT;
        }

        if (is_fast) _addictiveScore++;// 下に入れて、落ちれるときはボーナス追加

        return true;
    }
    void Control()
    {
        // 落とす
        if (!Fall(_logicalInput.IsRaw(LogicalInput.Key.Down))) return;
        // アニメーション中はキー入力を受け付けない
        if (_animationController.Update()) return;
        // 並行移動のキー入力取得
        if (_logicalInput.IsRepeat(LogicalInput.Key.Right))
        {
            if (Translate(true)) return;
        }
        if (_logicalInput.IsRepeat(LogicalInput.Key.Left))
        {
            if (Translate(false)) return;
        }

        // 回転のキー入力取得
        if (_logicalInput.IsTrigger(LogicalInput.Key.RotR))// 右回転
        {
            if (Rotate(true)) return;
        }
        if (_logicalInput.IsTrigger(LogicalInput.Key.RotL))// 左回転
        {
            if (Rotate(false)) return;
        }

        // クイックドロップのキー入力取得
        if (_logicalInput.IsRelease(LogicalInput.Key.QuickDrop))
        {
            QuickDrop();
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // 操作を受けて動かす
        Control();

        // 表示
        Vector3 dy = Vector3.up * (float)_fallCount / (float)FALL_COUNT_UNIT;
        float anim_rate = _animationController.GetNormalized();
        _puyoControllers[0].SetPos(dy + Interpole(_position, RotState.Invalid, _last_position, RotState.Invalid, anim_rate));
        _puyoControllers[1].SetPos(dy + Interpole(_position, _rotate, _last_position, _last_rotate, anim_rate));
    }

    static Vector3 Interpole(Vector2Int pos, RotState rot, Vector2Int pos_last, RotState rot_last, float rate)
    {
        // 並行移動
        Vector3 p = Vector3.Lerp(
            new Vector3((float)pos.x, (float)pos.y, 0.0f),
            new Vector3((float)pos_last.x, (float)pos_last.y, 0.0f), rate);

        if (rot == RotState.Invalid) return p;

        // 回転
        float theta0 = 0.5f * Mathf.PI * (float)(int)rot;
        float theta1 = 0.5f * Mathf.PI * (float)(int)rot_last;
        float theta = theta1 - theta0;

        // 近い方に回る
        if (+Mathf.PI < theta) theta = theta - 2.0f * Mathf.PI;
        if (theta < -Mathf.PI) theta = theta + 2.0f * Mathf.PI;

        theta = theta0 + rate * theta;

        return p + new Vector3(Mathf.Sin(theta), Mathf.Cos(theta), 0.0f);
    }

    public uint popScore()
    {
        uint score = _addictiveScore;
        _addictiveScore = 0;

        return score;
    }
}
