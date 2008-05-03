#ifndef PWIZ_API_DECL
#ifdef PWIZ_DYN_LINK
#ifdef PWIZ_SOURCE
#define PWIZ_API_DECL __declspec(dllexport)
#else
#define PWIZ_API_DECL __declspec(dllimport)
#endif  // PWIZ_SOURCE
#else
#define PWIZ_API_DECL
#endif  // PWIZ_DYN_LINK
#endif  // PWIZ_API_DECL
