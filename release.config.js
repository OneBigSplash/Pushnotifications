module.exports={
    plugins: [
      ["@semantic-release/commit-analyzer", {
          releaseRules: [
              {"type": "major"  , "release": "major"},
              {"type": "release", "release": "major"},
          ],
          parserOpts: {
              "noteKeywords": ["BREAKING CHANGE", "BREAKING CHANGES", "BREAKING"]
          }
      }],
  
      ["@semantic-release/exec",{
          prepareCmd: `CR=unicominternal.azurecr.io \
           && docker login $CR -u $DOCKER_HUB_USER -p $DOCKER_HUB_PASSWORD \
           && docker build -f ci/Dockerfile.api          -t $CR/unicominternal/pushnotifications.api:\${nextRelease.version}      $LOCAL_PATH \
           && docker build -f ci/Dockerfile.svc      -t $CR/unicominternal/pushnotifications.svc:\${nextRelease.version}  $LOCAL_PATH \
           && docker push $CR/unicominternal/pushnotifications.api:\${nextRelease.version} \
           && docker push $CR/unicominternal/pushnotifications.svc:\${nextRelease.version} \
           && docker logout \
          `,
      }],
      "@semantic-release/git"
    ],
  
    branches: [
      'master',
      { name: 'release-*', prerelease: true },
      { name: 'preview*', prerelease: true },
    ],
  }
  