# Contributing

Thank you for your interest in contributing to Appointment & Reservation System!

## Getting Started

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes
4. Add tests for any new business logic
5. Run the test suite: `dotnet test`
6. Commit your changes: `git commit -m "feat: add your feature"`
7. Push to your branch: `git push origin feature/your-feature-name`
8. Open a Pull Request

## Code Style

- Follow existing patterns: 3-layer architecture (Data → Service → Web)
- All SQL queries must be parameterized — no string concatenation
- New service methods should have corresponding unit tests (xUnit + Moq)
- Repository methods should support `CancellationToken`

## Pull Request Guidelines

- Keep PRs focused on a single concern
- Include a clear description of what changed and why
- Ensure all existing tests pass
- Add tests for new features or bug fixes

## Reporting Issues

Please use [GitHub Issues](../../issues) to report bugs or request features.
