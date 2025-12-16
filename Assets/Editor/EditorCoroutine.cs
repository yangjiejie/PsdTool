//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEditor;
//using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public abstract class EditorYieldInstruction
{
    /// <summary>
    /// 每帧检查，返回 true 表示等待结束，可以继续执行协程
    /// </summary>
    public abstract bool KeepWaiting { get; }
}

public class EditorWaitForSeconds : EditorYieldInstruction
{
    private readonly double m_WaitUntilTime;

    public EditorWaitForSeconds(float seconds)
    {
        m_WaitUntilTime = EditorApplication.timeSinceStartup + seconds;
    }

    public override bool KeepWaiting => EditorApplication.timeSinceStartup < m_WaitUntilTime;
}

public class EditorWaitUntil : EditorYieldInstruction
{
    private readonly Func<bool> m_Predicate;

    public EditorWaitUntil(Func<bool> predicate)
    {
        m_Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public override bool KeepWaiting => !m_Predicate();
}

public class EditorCoroutine
{
    private readonly IEnumerator m_Routine;
    private readonly Stack<IEnumerator> m_RoutineStack; // 支持嵌套协程

    public EditorCoroutine(IEnumerator routine)
    {
        m_Routine = routine;
        m_RoutineStack = new Stack<IEnumerator>();
        m_RoutineStack.Push(routine);
    }

    public static EditorCoroutine StartCoroutine(IEnumerator routine)
    {
        if (routine == null) return null;

        var editorCoroutine = new EditorCoroutine(routine);
        editorCoroutine.Start();
        return editorCoroutine;
    }

    public static void StopCoroutine(EditorCoroutine coroutine)
    {
        coroutine?.Stop();
    }

    private void Start()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    private void Stop()
    {
        EditorApplication.update -= Update;
        m_RoutineStack.Clear();
    }

    private void Update()
    {
        if (m_RoutineStack.Count == 0)
        {
            Stop();
            return;
        }

        var currentRoutine = m_RoutineStack.Peek();

        bool isDone;
        try
        {
            isDone = !currentRoutine.MoveNext();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            isDone = true;
        }

        if (isDone)
        {
            // 当前协程结束，弹出栈
            m_RoutineStack.Pop();
        }
        else
        {
            // 检查当前 yield return 的对象
            var yieldInstruction = currentRoutine.Current;

            switch (yieldInstruction)
            {
                case null:
                    // yield return null; 等待一帧
                    break;

                case IEnumerator nestedRoutine:
                    // yield return StartCoroutine(...); 嵌套协程
                    m_RoutineStack.Push(nestedRoutine);
                    break;

                case EditorYieldInstruction yi:
                    // 只要还在等，就什么都不做；下一帧再来检查
                    if (yi.KeepWaiting)
                        return;               // 直接结束本轮 Update，保持栈顶协程不动
                                              // KeepWaiting==false 表示已就绪，下一帧就会继续 MoveNext()
                    break;

                default:
                    // 未知类型，视为等待一帧
                    Debug.LogWarning($"EditorCoroutine: Unsupported yield instruction type: {yieldInstruction.GetType().Name}");
                    break;
            }
        }
    }
}