Daytona Integration CI
======================

This repository includes a dedicated GitHub Actions workflow to run the full
Daytona integration test suite which exercises external infrastructure,
containerized services, and long-running end-to-end scenarios.

Files
-----

- .github/workflows/daytona-integration.yml - scheduled and manual workflow that runs the full integration suite.
- .github/workflows/ci.yml - standard CI; can be toggled to skip long-running integration tests by setting the SKIP_DAYTONA_INTEGRATION environment variable to true.

Behavior
--------

The main CI pipeline (ci.yml) runs a filtered set of fast unit and short
tests by default. When SKIP_DAYTONA_INTEGRATION is set to true in the
workflow environment, the pipeline will explicitly exclude tests that are
marked with Category attributes such as ExternalInfrastructure, SlowIntegration,
and ContainerizedMock.

The Daytona integration workflow runs the test category `DaytonaIntegration` and
uploads test results as artifacts. The workflow validates Docker availability
before running Docker-based steps and does not rely on docker:dind by default.
It's scheduled to run daily and can also be triggered manually via the GitHub
Actions UI.

Runner requirements and recommendations
--------------------------------------

- Runner labels: The Daytona workflow requires a self-hosted Linux runner with the following labels: `self-hosted`, `linux`, `daytona`. This ensures jobs are routed to machines intended for long-running integration work.
- Docker & privileged containers: By default the workflow validates the presence of the `docker` CLI and uses the host's Docker; it does not enable docker:dind. If your custom scenario requires privileged DinD, configure dedicated self-hosted runners that allow privileged containers and document the risk: privileged containers increase host attack surface and may allow container escapes to compromise the runner.
- Resource recommendations: We recommend a machine with at least 4 CPU cores, 8 GB RAM, and 50 GB disk for typical Daytona scenarios; increase resources for heavier workloads or parallel container orchestration.

Required secrets and configuration
----------------------------------

- SHARPCLAW_DAYTONA_API_KEY (required): API key used by the Daytona test harness to authenticate against the Daytona control plane.
- SHARPCLAW_DAYTONA_DB_PASSWORD (required): Password for the test database used by integration tests.
- SHARPCLAW_DAYTONA_S3_ACCESS_KEY (recommended / default exists): S3 access key used by the MinIO test instance. A sensible default of "daytona" is used when this variable is not provided, but CI should set this secret to avoid using defaults. When provided, the value must be at least 3 characters long.
- SHARPCLAW_DAYTONA_S3_SECRET_KEY (recommended / default exists): Secret key for any S3-compatible storage used during tests. A default exists (not printed in logs) but CI should set the secret; when provided it must be at least 8 characters long.

Additional S3 credential constraints
----------------------------------

The Daytona fixtures validate minimal shapes for S3 credentials to avoid starting the full
integration topology with clearly invalid values. When providing secrets to CI or local
runners ensure:

- SHARPCLAW_DAYTONA_S3_ACCESS_KEY, when provided, is at least 3 characters long. If absent a default of "daytona" will be used (a warning is emitted).
- SHARPCLAW_DAYTONA_S3_SECRET_KEY, when provided, is at least 8 characters long. If absent a default will be used (a warning is emitted); the secret value itself is never printed to logs.

If running locally you can export values in your shell before running the tests, for example:

```bash
export SHARPCLAW_DAYTONA_S3_ACCESS_KEY=myaccess
export SHARPCLAW_DAYTONA_S3_SECRET_KEY=myverysecretkey
```

Short purpose summary:
- SHARPCLAW_DAYTONA_API_KEY: authentication to the Daytona control plane and API operations performed by tests.
- SHARPCLAW_DAYTONA_DB_PASSWORD: credentials for the test database instance used by integration test fixtures.
- SHARPCLAW_DAYTONA_S3_SECRET_KEY: secret key used with S3-compatible storage for uploading or reading test artifacts.

Optional overrides:

- You may provide alternate container image names or registry credentials via workflow inputs or repository secrets to point tests at internal images. You can also adjust timeout values for long-running steps via the workflow YAML or repository-level workflow settings.

.NET SDK requirement
--------------------

The Daytona integration suite targets .NET 10. Minimum tested SDK: .NET 10 (10.0.103). You can pin an exact SDK using a global.json file in the repository root to ensure consistent behavior across runners and developer machines.
Note: The workflow sets up .NET using the `actions/setup-dotnet` action with dotnet-version: '10.0.x' to prefer .NET 10 if a global.json is not present.

Security considerations
-----------------------

- Privileged DinD: Enabling privileged containers (DinD) on a self-hosted runner increases the attack surface — a container escape can give elevated access to the host. Only enable privileged mode on runners you control and trust.
 - Artifact uploads: Uploaded artifacts may contain sensitive test logs or secrets if not properly redacted. The Daytona workflow uploads artifacts only on failure or cancellation to limit exposure and retention; ensure only intended artifacts are collected and limit artifact retention where appropriate.

Authoring notes
---------------

- Keep the Daytona tests in a separate category to make filtering simple.
- Avoid running Daytona tests on every PR to keep feedback fast; rely on the
  scheduled run and a manual trigger for investigative runs.
