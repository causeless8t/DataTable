using System;
using UnityEditor;
using UnityEngine;

namespace Causeless3t.Table
{
    [InitializeOnLoad]
    internal static class TableGenerationResumeHandler
    {
        static TableGenerationResumeHandler()
        {
            EditorApplication.delayCall += TryResume;
        }

        private static void TryResume()
        {
            if (!TableGenerationState.WaitingForCompilation)
                return;

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryResume;
                return;
            }

            try
            {
                TableGenerator.GenerateEncryptedTables();

                TableGenerationState.Clear();

                Debug.Log("테이블 코드 생성 및 암호화 데이터 생성이 완료되었습니다.");
            }
            catch (Exception e)
            {
                TableGenerationState.Clear();
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}