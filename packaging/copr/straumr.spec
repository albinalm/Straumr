Name:           straumr
Version:        %{VERSION_PLACEHOLDER}
Release:        1%{?dist}
Summary:        CLI tool for managing, saving, and sending HTTP requests across workspaces
License:        GPL-3.0-only
URL:            https://github.com/albinalm/Straumr
Source0:        https://github.com/albinalm/Straumr/releases/download/v%{version}/straumr-%{version}-linux-x64.tar.gz

ExclusiveArch:  x86_64

%description
Straumr is a CLI tool for managing and sending HTTP requests. You define
requests once, save them to a workspace, and run them whenever you need.
It handles authentication, secrets, and multiple workspaces, so you can
keep work, staging, and personal projects separate without duplicating config.

%prep
%setup -c -T
tar xzf %{SOURCE0}

%install
install -Dm755 straumr %{buildroot}%{_bindir}/straumr

%files
%{_bindir}/straumr
