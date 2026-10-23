# [실험적 기능 로드맵] AI 딥러닝 초해상화 업스케일러 (Waifu2x / Real-ESRGAN)

> **상태**: 향후 구현 예정 시험적 기능 (Backlog / Experimental Roadmap)  
> **관련 논의**: 저사양 미니 PC(N100 등) 및 외장 GPU 미탑재 환경에서의 딜레이/발열 방지를 위해 경량 네이티브 업스케일러(스마트 엣지 FSR / 만화 펜선 복원 Anime4K)를 우선 탑재하고, 고사양 외장 GPU 사용자용 옵션으로 검토.

---

## 1. 개요 및 목표
* **목표**: 저해상도(600~1000px) 구작 만화나 오래된 일러스트를 인공지능 합성곱 신경망(CNN)을 통해 2배~4배(4K급)로 새로 그리듯 디테일을 복원하는 초해상화(Super-Resolution) 엔진.
* **대상 모델 후보**:
  1. **Waifu2x (Upconv7 / CUNet)**: 2D 애니메이션, 만화 그림 전용 노이즈 제거 및 2x 업스케일
  2. **Real-ESRGAN (Anime-6B)**: 강력한 디테일 복원 및 초고화질 재구성
  3. **Anime4K (Heavy Denoise / Upscale)**: 실시간 셰이더 기반 신경망

---

## 2. 하드웨어 요구사항 및 보류 이유
| 항목 | 사양 요구치 | 보류 배경 |
| :--- | :--- | :--- |
| **GPU** | NVIDIA GeForce RTX 3050 이상 권장<br>(CUDA / DirectML Tensor 코어) | 인텔 내장 그래픽(UHD, N100)으로 구동 시 **페이지당 3~8초** 지연 발생 |
| **VRAM / RAM** | VRAM 4GB 이상 / RAM 8GB 이상 | 고해상도 처리 중 VRAM 부족 시 Out-Of-Memory 발생 위험 |
| **프로그램 용량** | AI 모델 가중치 파일 (약 100MB ~ 300MB) | 현재 냐뷰의 **10MB 무설치 초경량** 철학과의 충돌 (별도 다운로드 플러그인화 필요) |

---

## 3. 향후 구현 시 설계 방향
1. **플러그인 / 외장 모델 분리**:
   * 기본 `nyaa-view.exe`는 10MB 경량을 유지하고, AI 업스케일러는 필요 시 사용자가 모델 파일(`model.onnx`)을 추가하는 **선택적 플러그인(Sidecar / ONNX DirectML)** 구조 채택.
2. **비동기 백그라운드 프리캐싱 연동**:
   * 1장당 0.5초~1초가 걸리므로, 현재 보고 있는 페이지 뒤의 3~5장을 백그라운드 워커 스레드에서 미리 AI 업스케일링하여 캐시에 적재(Precache)함으로써 책 넘김 딜레이를 최소화.
3. **DirectML 기반 윈도우 네이티브 가속**:
   * 별도 무거운 Python/PyTorch 환경 없이 Windows 11에 내장된 `DirectML.dll` + `Microsoft.ML.OnnxRuntime` 단일 라이브러리로 네이티브 GPU 가속 구현.
