#pragma once

#include "secret_store.h"

namespace preprovision_client
{
  enum Result
  {
    SUCCESS,
    NETWORK_ERROR,
    SIGNATURE_REJECTED,
    SERVER_ERROR,
    LOCAL_ERROR
  };

  Result verify(const DeviceCredentials &creds);
}
