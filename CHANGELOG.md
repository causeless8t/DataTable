# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Fixed
- 생성 소스가 최신이어도 이전 스키마 타입이 로드되어 있으면 컴파일 및 Domain Reload 후 테이블 변환 재개
- 스키마 서명으로 컬럼 이름·타입·순서를 검증하여 이전 직렬화 코드 사용 방지
- Editor 회귀 테스트 추가(오래된 타입, 컬럼 추가/타입 변경/순서 변경, 동일 소스 재작성 방지)

## [1.1.1] - 2026-09-21
### Fixed
- DataTableSettings의 경로를 프로젝트 상대경로(`Assets/...`)로 저장하도록 수정
- 기존 절대경로 설정을 프로젝트 상대경로로 자동 변환
- ResourcesDataLoader가 운영체제와 무관한 Resources 경로를 사용하도록 수정

## [1.1.0] - 2026-09-10
### Modified
- DataConverter
  - CSV > 코드 생성으로 로직 변경
  - 프로젝트 구조 변경

## [1.0.1] - 2024-12-19
### Modified
- DataConverter
  - 엑셀 파일로 읽을 수 있도록 수정

## [1.0.0] - 2023-10-01
### Added
- TableDataParser
  - 로거 패키지 교체
